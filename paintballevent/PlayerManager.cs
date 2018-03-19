using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rocket.API;
using Rocket.Core;
using Rocket.Unturned;
using SDG.Unturned;
using Rocket.Unturned.Chat;
using Steamworks;
using Rocket.Unturned.Items;
using Rocket.Core.Logging;

namespace datathegenius.paintballevent
{
    public class PlayerManager
    {
        //Player details.
        public UnturnedPlayer player;
        string name;
        CSteamID id;
        CSteamID gId;
        Vector3 location;
        float rotation;
        uint experienceOnJoin;
        List<ItemJar> playerItems;
        List<ItemJar> playerClothing;

        //Other
        int wins = 0;
        Boolean revived = true;
        Boolean dead = false;

        public PlayerManager(UnturnedPlayer tempPlayer)
        {
            player = tempPlayer;
            name = tempPlayer.CharacterName;
            id = tempPlayer.CSteamID;
            gId = tempPlayer.SteamGroupID;
            experienceOnJoin = player.Experience;
            playerItems = new List<ItemJar>();
            playerClothing = new List<ItemJar>();

            location = tempPlayer.Position;
            rotation = tempPlayer.Rotation;
            revived = true;
            dead = false;

           
        }

        public void updatePlayer(UnturnedPlayer tempPlayer)
        {
            player = tempPlayer;
        }

        public Boolean getRevived()
        {
            return revived;
        }

        public void setRevived(Boolean status)
        {
            revived = status;
        }

        public void increaseWins()
        {
            wins++;
        }

        public void setWins(int tempWins)
        {
            wins = tempWins;
        }

        public UnturnedPlayer getPlayer()
        {
            return player;
        }
        public int getWins()
        {
            return wins;
        }

        public string getName()
        {
            return name;
        }

        public CSteamID getSteamID()
        {
            return id;
        }

        public CSteamID getGroupID()
        {
            return gId;
        }

        public Vector3 getLocation()
        {
            return location;
        }

        public float getRotation()
        {
            return rotation;
        }
        public Boolean getDead()
        {
            return dead;
        }

        public void setDead(Boolean died)
        {
            dead = died;
        }

        public void saveAndClearInventory()
        {
            //So let's convert each SteamPlayer into an UnturnedPlayer
            UnturnedPlayer tempPlayer = UnturnedPlayer.FromCSteamID(player.CSteamID);

            var playerInventory = tempPlayer.Inventory;

            // "Remove "models" of items from player "body""
            tempPlayer.Player.channel.send("tellSlot", ESteamCall.ALL, ESteamPacket.UPDATE_RELIABLE_BUFFER, (byte)0, (byte)0, new byte[0]);
            tempPlayer.Player.channel.send("tellSlot", ESteamCall.ALL, ESteamPacket.UPDATE_RELIABLE_BUFFER, (byte)1, (byte)0, new byte[0]);

            // Remove items
            for (byte page = 0; page < 8; page++)
            {
                var count = playerInventory.getItemCount(page);

                for (byte index = 0; index < count; index++)
                {
                    playerItems.Add(playerInventory.getItem(page, 0));
                    playerInventory.removeItem(page, 0);
                }
            }

            // Remove clothes

            // Remove unequipped cloths
            System.Action removeUnequipped = () =>
            {
                for (byte i = 0; i < playerInventory.getItemCount(2); i++)
                {
                    if (playerInventory.getItem(2, 0) != null)
                        playerClothing.Add(playerInventory.getItem(2, 0));

                        playerInventory.removeItem(2, 0);
                }
            };

            // Unequip & remove from inventory
            tempPlayer.Player.clothing.askWearBackpack(0, 0, new byte[0], true);

            removeUnequipped();

            tempPlayer.Player.clothing.askWearGlasses(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearHat(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearPants(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearMask(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearShirt(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearVest(0, 0, new byte[0], true);
            removeUnequipped();

            //Clear player experience.
            tempPlayer.Experience = 0;
        }

        public void returnInventory()
        {
            Boolean itemsDropped = false;
            //Return clothing.
            for(int x = 0; x < playerClothing.Count(); x++)
            {
                player.Inventory.tryAddItem(playerClothing[x].item, true);
            }
              //Return items.
              for (int x = 0; x < playerItems.Count(); x++)
              {
                try
                {
                    if (!player.Inventory.tryAddItem(playerItems[x].item, true))
                    {
                        ItemManager.dropItem(playerItems[x].item, location, true, true, true);
                        itemsDropped = true;
                    }
                }
                catch(NullReferenceException e)
                {
                    Logger.Log(e.StackTrace);
                }
                   
              }

            //Let user know if items were dropped.         
            if(itemsDropped) 
                UnturnedChat.Say(player, "Some of your items were dropped below you.", Color.cyan);

            //Give back experience.. Letting them keep the max skills.
            player.Experience = experienceOnJoin;
        }

        //Static methods
        public static int duplicatePlayerGroupCheck(UnturnedPlayer tempPlayer)
        {
            CSteamID steamID = tempPlayer.CSteamID;
            CSteamID groupID = tempPlayer.SteamGroupID;

            //Duplicate player check.
            var duplicatePlayersCheck = PaintballManager.joinedPlayers.FirstOrDefault(item => item.getSteamID() == steamID);

            if (duplicatePlayersCheck != null)
            {
                UnturnedChat.Say(tempPlayer, "You've already joined paintball.", Color.red);
                return 0;
            }

            //Duplicate group check.
            var duplicateGroupCheck = PaintballManager.joinedPlayers.FirstOrDefault(item => item.getGroupID() == groupID);

            if(duplicateGroupCheck != null && groupID.ToString() != "0")
            {
                UnturnedChat.Say(tempPlayer, "One of your group members has already joined, leave your group to join.", Color.red);
                return 1;
            }
            return -1;
        }

        public static PlayerManager findPlayerManagerObject(UnturnedPlayer tempPlayer)
        {
            CSteamID steamID = tempPlayer.CSteamID;

            //Find the player
            var playerManagerObj = PaintballManager.joinedPlayers.FirstOrDefault(item => item.getSteamID() == steamID);

            return playerManagerObj;
        }

        public static void clearInventory(UnturnedPlayer tempPlayer)
        {
            var playerInventory = tempPlayer.Inventory;

            // "Remove "models" of items from player "body""
            tempPlayer.Player.channel.send("tellSlot", ESteamCall.ALL, ESteamPacket.UPDATE_RELIABLE_BUFFER, (byte)0, (byte)0, new byte[0]);
            tempPlayer.Player.channel.send("tellSlot", ESteamCall.ALL, ESteamPacket.UPDATE_RELIABLE_BUFFER, (byte)1, (byte)0, new byte[0]);

            // Remove items
            for (byte page = 0; page < 8; page++)
            {
                var count = playerInventory.getItemCount(page);

                for (byte index = 0; index < count; index++)
                {
                    playerInventory.removeItem(page, 0);
                }
            }

            // Remove clothes

            // Remove unequipped cloths
            System.Action removeUnequipped = () =>
            {
                for (byte i = 0; i < playerInventory.getItemCount(2); i++)
                {
                    playerInventory.removeItem(2, 0);
                }
            };
            // Unequip & remove from inventory
            tempPlayer.Player.clothing.askWearBackpack(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearGlasses(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearHat(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearPants(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearMask(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearShirt(0, 0, new byte[0], true);
            removeUnequipped();

            tempPlayer.Player.clothing.askWearVest(0, 0, new byte[0], true);
            removeUnequipped();
        }

        //Give max skills to a player.
        public static void maxSkills(UnturnedPlayer tempPlayer)
        {
            var pSkills = tempPlayer.Player.skills;

            Boolean overpower = false;
            foreach (var skill in pSkills.skills.SelectMany(skArr => skArr))
            {
                skill.level = overpower ? byte.MaxValue : skill.max;
            }
            pSkills.askSkills(tempPlayer.CSteamID);
        }

        //Find players by name.
        public static UnturnedPlayer findPlayer(IRocketPlayer caller, String userInput)
        {
            foreach (SteamPlayer plr in Provider.Players)
            {
                //Convert each SteamPlayer into an UnturnedPlayer
                UnturnedPlayer unturnedPlayer = UnturnedPlayer.FromSteamPlayer(plr);

                if (unturnedPlayer.DisplayName.ToLower().IndexOf(userInput.ToLower()) != -1 || unturnedPlayer.CharacterName.ToLower().IndexOf(userInput.ToLower()) != -1 || unturnedPlayer.SteamName.ToLower().IndexOf(userInput.ToLower()) != -1 || unturnedPlayer.CSteamID.ToString().Equals(userInput))
                {
                    return unturnedPlayer;
                }
            }
            UnturnedChat.Say(caller, "Did not find anyone with the name \"" + userInput + "\".", Color.red);
            return null;
        }
    }
}
