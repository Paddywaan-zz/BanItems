using BepInEx;
using RoR2;
using UnityEngine;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.Reflection;

//Thanks to Atlas_, Elysium
namespace Paddywan
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Paddywan.BanItem", "Ban Items, Equipment, Lunar from droplists.", "1.0.1")]
    public class BanItems : BaseUnityPlugin
    {
        private static ConfigWrapper<bool>[] icBanned = new ConfigWrapper<bool>[(int)ItemIndex.Count]; //itemConfigWrappers
        public static List<ItemIndex> iblackList = new List<ItemIndex>(); //ItemBlacklist
        private static ConfigWrapper<bool>[] ecBanned = new ConfigWrapper<bool>[(int)EquipmentIndex.Count]; //EquipmentConfigWrappers
        public static List<EquipmentIndex> eblackList = new List<EquipmentIndex>(); //EquipmentBlacklist

        public void Awake()
        {
            //Setup banned items & equipment
            initConf();

            //Don't know what this does but we need it.
            On.RoR2.Console.Awake += (orig, self) =>
            {
                CommandHelper.RegisterCommands(self);

                orig(self);
            };

            /*
             * We are completely rewriting BuildDropTable to avoid using IL. During various tests it was discovered that
             * modifying the tierDropTables directly resulted in client sync issues, which would make clients unable to join
             * the lobby, although the changes were working fine in singleplayer. Similarly amonst other tests we found that
             * using a standard eventhook didn't really work as we would modify the droplist before it was created, this 
             * removing non-existant entries from the droplist; or we would modify it too late and the list would be readOnly
             * and changes would not be effective. The following solution appears to allow clients to join a lobby without
             * having the mod installed.
             * Massive thanks to Atlas_ and the entire modding discord for their help and support. Without Atlas_ this hook
             * would not have been possible.
             */
            On.RoR2.Run.BuildDropTable += (On.RoR2.Run.orig_BuildDropTable orig, Run self) =>
            {
                self.availableTier1DropList.Clear();
                self.availableTier2DropList.Clear();
                self.availableTier3DropList.Clear();
                self.availableLunarDropList.Clear();
                self.availableEquipmentDropList.Clear();
                
                List<ItemIndex> itemList = new List<ItemIndex>();
                List<EquipmentIndex> equipList = new List<EquipmentIndex>();
                /*
                 * Loop through ItemsIndex and omit adding banned items to the itemDropList.
                 */
                for (ItemIndex itemIndex = ItemIndex.Syringe; itemIndex < ItemIndex.Count; itemIndex++)
                {
                    if (iblackList.IndexOf(itemIndex) < 0)
                    {
                        itemList.Add(itemIndex);
                    }
                }
                foreach (ItemIndex itemIndex in itemList)
                {
                    if (self.availableItems.HasItem(itemIndex))
                    {
                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                        List<PickupIndex> list = null;
                        switch (itemDef.tier)
                        {
                            case ItemTier.Tier1:
                                list = self.availableTier1DropList;
                                break;
                            case ItemTier.Tier2:
                                list = self.availableTier2DropList;
                                break;
                            case ItemTier.Tier3:
                                list = self.availableTier3DropList;
                                break;
                            case ItemTier.Lunar:
                                list = self.availableLunarDropList;
                                break;
                        }
                        if (list != null)
                        {
                            list.Add(new PickupIndex(itemIndex));
                        }
                    }
                }
                /*
                 * Loop through EquipmentIndex and omit adding banned equipment to the eqipmentDropList.
                 */
                for (EquipmentIndex equipIndex = EquipmentIndex.CommandMissile; equipIndex < EquipmentIndex.Count; equipIndex++)
                {
                    if (eblackList.IndexOf(equipIndex) < 0)
                    {
                        equipList.Add(equipIndex);
                    }
                }
                foreach (EquipmentIndex equipmentIndex in equipList)
                {
                    if (self.availableEquipment.HasEquipment(equipmentIndex))
                    {
                        EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                        if (equipmentDef.canDrop)
                        {
                            if (!equipmentDef.isLunar)
                            {
                                self.availableEquipmentDropList.Add(new PickupIndex(equipmentIndex));
                            }
                            else
                            {
                                self.availableLunarDropList.Add(new PickupIndex(equipmentIndex));
                            }
                        }
                    }
                }
                self.smallChestDropTierSelector.Clear();
                self.smallChestDropTierSelector.AddChoice(self.availableTier1DropList, 0.8f);
                self.smallChestDropTierSelector.AddChoice(self.availableTier2DropList, 0.2f);
                self.smallChestDropTierSelector.AddChoice(self.availableTier3DropList, 0.01f);
                self.mediumChestDropTierSelector.Clear();
                self.mediumChestDropTierSelector.AddChoice(self.availableTier2DropList, 0.8f);
                self.mediumChestDropTierSelector.AddChoice(self.availableTier3DropList, 0.2f);
                self.largeChestDropTierSelector.Clear();
            };
        }

        public void Update()
        {
#if DEBUG
            itemSpawnHelper();
#endif
        }

        /*
         * BoilerPlate's itemSpawner for testing active droptables.
         */
        private void itemSpawnHelper()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                //We grab a list of all available Tier 1 drops:
                var dropList = Run.instance.availableTier1DropList;

                //Randomly get the next item:
                var nextItem = Run.instance.treasureRng.RangeInt(0, dropList.Count);

                //Get the player body to use a position:
                var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                //And then finally drop it infront of the player.
                PickupDropletController.CreatePickupDroplet(dropList[nextItem], transform.position, transform.forward * 20f);
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                //We grab a list of all available Tier 2 drops:
                var dropList = Run.instance.availableTier2DropList;

                //Randomly get the next item:
                var nextItem = Run.instance.treasureRng.RangeInt(0, dropList.Count);

                //Get the player body to use a position:
                var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                //And then finally drop it infront of the player.
                PickupDropletController.CreatePickupDroplet(dropList[nextItem], transform.position, transform.forward * 20f);
            }
            //This if statement checks if the player has currently pressed F2, and then proceeds into the statement:
            if (Input.GetKeyDown(KeyCode.F4))
            {
                //We grab a list of all available Tier 3 drops:
                var dropList = Run.instance.availableTier3DropList;

                //Randomly get the next item:
                var nextItem = Run.instance.treasureRng.RangeInt(0, dropList.Count);

                //Get the player body to use a position:
                var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                //And then finally drop it infront of the player.
                PickupDropletController.CreatePickupDroplet(dropList[nextItem], transform.position, transform.forward * 20f);
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                //We grab a list of all available Equipment drops:
                var dropList = Run.instance.availableEquipmentDropList;

                //Randomly get the next item:
                var nextItem = Run.instance.treasureRng.RangeInt(0, dropList.Count);

                //Get the player body to use a position:
                var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                //And then finally drop it infront of the player.
                PickupDropletController.CreatePickupDroplet(dropList[nextItem], transform.position, transform.forward * 20f);
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                //We grab a list of all available Lunar drops:
                var dropList = Run.instance.availableLunarDropList;

                //Randomly get the next item:
                var nextItem = Run.instance.treasureRng.RangeInt(0, dropList.Count);

                //Get the player body to use a position:
                var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                //And then finally drop it infront of the player.
                PickupDropletController.CreatePickupDroplet(dropList[nextItem], transform.position, transform.forward * 20f);
            }
        }

        /*
         * Create & read config into blacklists.
         */
        private void initConf()
        {
            //Set Language for storing item names as their ingame English locale inside config comments.
            Language.LoadAllFilesForLanguage("EN_US");
            //Loop through itemIndex to generate config entry per item.
            for (ItemIndex ii = ItemIndex.Syringe; ii < ItemIndex.Count; ii++)
            {
                icBanned[(int)ii] = Config.Wrap(
                    "Items",
                    ii.ToString(),
                    Language.GetString(ItemCatalog.GetItemDef(ii).nameToken, "EN_US") + " Default Value = false",
                    false
                    );
                //Ban item if configured value is true.
                if (icBanned[(int)ii].Value == true)
                {
                    iblackList.Add(ii);
                }
            }
            //Loop through EquipmentIndex to generate config entry per item.
            for (EquipmentIndex ei = EquipmentIndex.CommandMissile; ei < EquipmentIndex.Count; ei++)
            {
                ecBanned[(int)ei] = Config.Wrap(
                    "Equipment",
                    ei.ToString(),
                    Language.GetString(EquipmentCatalog.GetEquipmentDef(ei).nameToken, "EN_US") + " Default Value = false",
                    false
                    );
                //Ban equipment if configured value is true.
                if(ecBanned[(int)ei].Value == true)
                {
                    eblackList.Add(ei);
                }
            }
        }
    }
}