﻿using System;

namespace XIVComboVeryExpandedPlugin {
	[AttributeUsage(AttributeTargets.Field)]
	internal class CustomComboInfoAttribute: Attribute {
		internal CustomComboInfoAttribute(string fancyName, string description, byte jobID, params uint[] actionIDs) {
			this.FancyName = fancyName;
			this.Description = description;
			this.JobID = jobID;
			this.ActionIDs = actionIDs;
		}

		public string FancyName { get; }
		public string Description { get; }
		public byte JobID { get; }
		public string JobName => jobIdToName(this.JobID);
		public uint[] ActionIDs { get; }

		private static string jobIdToName(byte key) {
			return key switch {
				1 => "Gladiator",
				2 => "Pugilist",
				3 => "Marauder",
				4 => "Lancer",
				5 => "Archer",
				6 => "Conjurer",
				7 => "Thaumaturge",
				8 => "Carpenter",
				9 => "Blacksmith",
				10 => "Armorer",
				11 => "Goldsmith",
				12 => "Leatherworker",
				13 => "Weaver",
				14 => "Alchemist",
				15 => "Culinarian",
				16 => "Miner",
				17 => "Botanist",
				18 => "Fisher",
				19 => "Paladin",
				20 => "Monk",
				21 => "Warrior",
				22 => "Dragoon",
				23 => "Bard",
				24 => "White Mage",
				25 => "Black Mage",
				26 => "Arcanist",
				27 => "Summoner",
				28 => "Scholar",
				29 => "Rogue",
				30 => "Ninja",
				31 => "Machinist",
				32 => "Dark Knight",
				33 => "Astrologian",
				34 => "Samurai",
				35 => "Red Mage",
				36 => "Blue Mage",
				37 => "Gunbreaker",
				38 => "Dancer",
				_ => "Unknown",
			};
		}
	}
}
