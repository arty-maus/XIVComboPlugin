using System;
using System.Linq;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Utility;

using XIVComboVX.Attributes;

namespace XIVComboVX.Combos {
	internal abstract class CustomCombo {
		#region static 

		public const uint InvalidObjectID = 0xE000_0000;

		public readonly string ModuleName;

		#endregion

		protected internal abstract CustomComboPreset Preset { get; }
		protected internal virtual uint[] ActionIDs { get; } = Array.Empty<uint>();

		protected byte JobID { get; }
		public byte ClassID => this.JobID switch {
			>= 19 and <= 25 => (byte)(this.JobID - 18),
			27 or 28 => 26,
			30 => 29,
			_ => this.JobID,
		};

		protected CustomCombo() {
			CustomComboInfoAttribute presetInfo = this.Preset.GetAttribute<CustomComboInfoAttribute>();
			this.JobID = presetInfo.JobID;
			this.ModuleName = this.GetType().Name;
		}

		public bool TryInvoke(uint actionID, uint lastComboActionId, float comboTime, byte level, out uint newActionID) {
			newActionID = 0;

			if (LocalPlayer is null
				|| (this.JobID != LocalPlayer.ClassJob.Id && this.ClassID != LocalPlayer.ClassJob.Id)
				|| (this.ActionIDs.Length > 0 && !this.ActionIDs.Contains(actionID))
				|| !IsEnabled(this.Preset)
			)
				return false;

			Service.Logger.debug($"{this.ModuleName}.Invoke({actionID}, {lastComboActionId}, {comboTime}, {level})");
			try {
				uint resultingActionID = this.Invoke(actionID, lastComboActionId, comboTime, level);
				if (resultingActionID == 0 || actionID == resultingActionID) {
					Service.Logger.debug("NO REPLACEMENT");
					return false;
				}

				Service.Logger.debug($"Became #{resultingActionID}");
				newActionID = resultingActionID;
				return true;
			}
			catch (Exception ex) {
				Service.Logger.error($"Error in {this.ModuleName}.Invoke({actionID}, {lastComboActionId}, {comboTime}, {level})", ex);
				return false;
			}
		}

		protected abstract uint Invoke(uint actionID, uint lastComboActionId, float comboTime, byte level);

		protected static uint PickByCooldown(uint original, params uint[] actions) {

			static (uint ActionID, CooldownData Data) Selector(uint actionID) => (actionID, GetCooldown(actionID));

			static (uint ActionID, CooldownData Data) Compare(uint original, (uint ActionID, CooldownData Data) a1, (uint ActionID, CooldownData Data) a2) {

				// VS decided that the conditionals could be "simplified" to this.
				// Someone should maybe teach VS what "simplified" actually means.
				(uint ActionID, CooldownData Data) choice =
					!a1.Data.IsCooldown && !a2.Data.IsCooldown
						? original == a1.ActionID // both off CD, return the original if possible, else the LAST one in the original call
							? a1
							: a2
						: a1.Data.IsCooldown && a2.Data.IsCooldown // one/both on CD
							? a1.Data.CooldownRemaining < a2.Data.CooldownRemaining // both on CD, return the one with less time left
								? a1
								: a2
							: a1.Data.IsCooldown // only one on CD, return the other
								? a2
								: a1;

				Service.Logger.debug($"CDCMP: {a1.ActionID}, {a2.ActionID}: {choice.ActionID}\n{a1.Data.DebugLabel}\n{a2.Data.DebugLabel}");
				return choice;
			}

			uint id = actions
				.Select(Selector)
				.Aggregate((a1, a2) => Compare(original, a1, a2))
				.ActionID;
			Service.Logger.debug($"Final selection: {id}");
			return id;
		}

		protected static uint SimpleChainCombo(byte level, uint last, float time, params (byte lvl, uint id)[] sequence) {
			if (time > 0) {
				// Work backwards, find the latest item in the chain that can be used (level >= lvl) and is ready to be used (last == prev.id)
				for (int i = sequence.Length - 1; i > 0; --i) {
					(byte lvl, uint id) = sequence[i];
					uint prev = sequence[i - 1].id;
					if (level >= lvl && prev == last)
						return id;
				}
			}

			// If nothing is found or we're not in a combo, then use the first item in the chain
			return sequence[0].id;
		}

		#region Utility/convenience getters

		protected internal static uint OriginalHook(uint actionID) => Service.IconReplacer.OriginalHook(actionID);

		protected internal static PlayerCharacter? LocalPlayer => Service.Client.LocalPlayer;

		protected internal static GameObject? CurrentTarget => Service.Targets.Target;

		protected internal static bool IsEnabled(CustomComboPreset preset) {
			if ((int)preset < 100) {
				Service.Logger.debug($"Bypassing is-enabled check for preset #{(int)preset}");
				return true;
			}
			bool enabled = Service.Configuration.IsEnabled(preset);
			Service.Logger.debug($"Checking status of preset #{(int)preset} - {enabled}");
			return enabled;
		}

		protected internal static bool HasCondition(ConditionFlag flag) => Service.Conditions[flag];

		protected internal static bool HasPetPresent() => Service.BuddyList.PetBuddyPresent;

		protected internal static CooldownData GetCooldown(uint actionID) => Service.DataCache.GetCooldown(actionID);

		protected internal static bool IsOnCooldown(uint actionID) => GetCooldown(actionID).IsCooldown;

		protected internal static bool IsOffCooldown(uint actionID) => !GetCooldown(actionID).IsCooldown;

		protected internal static T GetJobGauge<T>() where T : JobGaugeBase => Service.DataCache.GetJobGauge<T>();

		protected internal static bool ShouldSwiftcast
			=> IsOffCooldown(Common.Swiftcast)
				&& !SelfHasEffect(Common.Buffs.LostChainspell)
				&& !SelfHasEffect(RDM.Buffs.Dualcast);
		protected internal static bool IsFastcasting
			=> SelfHasEffect(Common.Buffs.Swiftcast1)
				|| SelfHasEffect(Common.Buffs.Swiftcast2)
				|| SelfHasEffect(Common.Buffs.Swiftcast3)
				|| SelfHasEffect(RDM.Buffs.Dualcast)
				|| SelfHasEffect(Common.Buffs.LostChainspell);
		protected internal static bool CanInterrupt
			=> Service.DataCache.CanInterruptTarget;

		#endregion

		#region Effects

		protected internal static Status? SelfFindEffect(ushort effectId) => FindEffect(effectId, LocalPlayer, null);
		protected internal static bool SelfHasEffect(ushort effectId) => SelfFindEffect(effectId) is not null;
		protected internal static float SelfEffectDuration(ushort effectId) => SelfFindEffect(effectId)?.RemainingTime ?? 0;
		protected internal static float SelfEffectStacks(ushort effectId) => SelfFindEffect(effectId)?.StackCount ?? 0;

		protected internal static Status? TargetFindAnyEffect(ushort effectId) => FindEffect(effectId, CurrentTarget, null);
		protected internal static bool TargetHasAnyEffect(ushort effectId) => TargetFindAnyEffect(effectId) is not null;
		protected internal static float TargetAnyEffectDuration(ushort effectId) => TargetFindAnyEffect(effectId)?.RemainingTime ?? 0;
		protected internal static float TargetAnyEffectStacks(ushort effectId) => TargetFindAnyEffect(effectId)?.StackCount ?? 0;

		protected internal static Status? TargetFindOwnEffect(ushort effectId) => FindEffect(effectId, CurrentTarget, LocalPlayer?.ObjectId);
		protected internal static bool TargetHasOwnEffect(ushort effectId) => TargetFindOwnEffect(effectId) is not null;
		protected internal static float TargetOwnEffectDuration(ushort effectId) => TargetFindOwnEffect(effectId)?.RemainingTime ?? 0;
		protected internal static float TargetOwnEffectStacks(ushort effectId) => TargetFindOwnEffect(effectId)?.StackCount ?? 0;

		protected internal static Status? FindEffect(ushort effectId, GameObject? actor, uint? sourceId)
			=> Service.DataCache.GetStatus(effectId, actor, sourceId);

		#endregion
	}
}