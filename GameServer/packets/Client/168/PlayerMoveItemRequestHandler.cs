using DOL.Database;
using DOL.Events;
using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerMoveItem, "Handle Moving Items Request", eClientStatus.PlayerInGame)]
    public class PlayerMoveItemRequestHandler : IPacketHandler
    {
        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            if (client.Player == null)
                return;

            _ = packet.ReadShort();
            eInventorySlot toClientSlot = (eInventorySlot) packet.ReadShort();
            eInventorySlot fromClientSlot = (eInventorySlot) packet.ReadShort();
            ushort itemCount = packet.ReadShort();

            // If `toClientSlot` is > 1000, then the target is a `GameObject` with an `ObjectID` of `toClientSlot` - 1000.
            if ((int) toClientSlot > 1000)
            {
                MoveItemToTargetObject(client, toClientSlot, fromClientSlot, itemCount);
                return;
            }

            // We did not drop an item on a game object, which means we should have valid from and to slots since we are moving an item from one window to another.

            // First check for an active `GameInventoryObject`.
            if (client.Player.ActiveInventoryObject?.MoveItem(client.Player, fromClientSlot, toClientSlot, itemCount) == true)
                return;

            bool isFromSlotValid = fromClientSlot is (>= eInventorySlot.Ground and <= eInventorySlot.LastBackpack) or
                                                     (>= eInventorySlot.FirstVault and <= eInventorySlot.LastVault) or
                                                     (>= eInventorySlot.FirstBagHorse and <= eInventorySlot.LastBagHorse);

            if (!isFromSlotValid)
            {
                client.Out.SendInventoryItemsUpdate(null);
                return;
            }

            bool isToSlotValid = toClientSlot is eInventorySlot.PlayerPaperDoll or
                                                 eInventorySlot.NewPlayerPaperDoll or
                                                 (>= eInventorySlot.Ground and <= eInventorySlot.LastBackpack) or
                                                 (>= eInventorySlot.FirstVault and <= eInventorySlot.LastVault) or
                                                 (>= eInventorySlot.FirstBagHorse and <= eInventorySlot.LastBagHorse);

            if (!isToSlotValid)
            {
                client.Out.SendInventoryItemsUpdate(null);
                return;
            }

            // Are we dropping the item?
            if (toClientSlot == eInventorySlot.Ground)
            {
                DropItem(client, fromClientSlot);
                return;
            }

            // Are we shift right clicking or dropping the item on the paper doll?
            if (toClientSlot is eInventorySlot.PlayerPaperDoll or eInventorySlot.NewPlayerPaperDoll)
            {
                EquipItemFromInventory(client, fromClientSlot, itemCount);
                return;
            }

            // We're simply moving the item from one inventory slot to another.
            client.Player.Inventory.MoveItem(fromClientSlot, toClientSlot, itemCount);
            return;
        }

        private static void MoveItemToTargetObject(GameClient client, eInventorySlot toClientSlot, eInventorySlot fromClientSlot, ushort itemCount)
        {
            ushort objectID = (ushort) (toClientSlot - 1000);
            GameObject obj = WorldMgr.GetObjectByIDFromRegion(client.Player.CurrentRegionID, objectID);

            if (obj == null || obj.ObjectState != GameObject.eObjectState.Active)
            {
                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                client.Out.SendMessage("Invalid trade target.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            GamePlayer tradeTarget = obj as GamePlayer;

            // If our target is another player we set the trade target.
            // trade permissions are checked in `GamePlayer`.
            if (tradeTarget != null)
            {
                if (tradeTarget.Client.ClientState != GameClient.eClientState.Playing)
                {
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    client.Out.SendMessage("Can't trade with inactive players.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (tradeTarget == client.Player)
                {
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    client.Out.SendMessage("You can't trade with yourself, silly!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (!GameServer.ServerRules.IsAllowedToTrade(client.Player, tradeTarget, false))
                {
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }
            }

            // Is the item we want to move in our backpack?
            // We also allow drag'n drop from equipped to blacksmith.
            if (fromClientSlot is >= eInventorySlot.FirstBackpack and <= eInventorySlot.LastBackpack ||
                (fromClientSlot is >= eInventorySlot.MinEquipable and <= eInventorySlot.MaxEquipable && obj is Blacksmith))
            {
                if (!obj.IsWithinRadius(client.Player, WorldMgr.GIVE_ITEM_DISTANCE))
                {
                    // show too far away message
                    if (obj is GamePlayer player)
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "PlayerMoveItemRequestHandler.TooFarAway", client.Player.GetName(player)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "PlayerMoveItemRequestHandler.TooFarAway", obj.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                DbInventoryItem item = client.Player.Inventory.GetItem(fromClientSlot);

                if (item == null)
                {
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                if (obj is not GameNPC || item.Count == 1)
                {
                    // See if any event handlers will handle this move.
                    client.Player.Notify(GamePlayerEvent.GiveItem, client.Player, new GiveItemEventArgs(client.Player, obj, item));

                    // If the item has been removed by the event handlers, return.
                    if (item == null || item.OwnerID == null)
                    {
                        client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                        return;
                    }
                }

                if (tradeTarget != null)
                {
                    // This is a player trade, let trade code handle.
                    tradeTarget.ReceiveTradeItem(client.Player, item);
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                if (obj.ReceiveItem(client.Player, item))
                {
                    // This object was expecting an item and handled it.
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                return;
            }

            // Is the "item" we want to move money? For Version 1.78+
            if (client.Version >= GameClient.eClientVersion.Version178 && fromClientSlot >= eInventorySlot.Mithril178 && fromClientSlot <= eInventorySlot.Copper178)
                fromClientSlot -= eInventorySlot.Mithril178 - eInventorySlot.Mithril;

            // Is the "item" we want to move money?
            if (fromClientSlot is >= eInventorySlot.Mithril and <= eInventorySlot.Copper)
            {
                int[] money = new int[5];
                money[fromClientSlot - eInventorySlot.Mithril] = itemCount;
                long flatMoney = Money.GetMoney(money[0], money[1], money[2], money[3], money[4]);

                if (client.Version >= GameClient.eClientVersion.Version178)
                    fromClientSlot += eInventorySlot.Mithril178 - eInventorySlot.Mithril;

                if (!obj.IsWithinRadius(client.Player, WorldMgr.GIVE_ITEM_DISTANCE))
                {
                    if (obj is GamePlayer player)
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "PlayerMoveItemRequestHandler.TooFarAway", client.Player.GetName(player)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "PlayerMoveItemRequestHandler.TooFarAway", obj.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                if (flatMoney > client.Player.GetCurrentMoney())
                {
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                client.Player.Notify(GamePlayerEvent.GiveMoney, client.Player, new GiveMoneyEventArgs(client.Player, obj, flatMoney));

                if (tradeTarget != null)
                {
                    tradeTarget.ReceiveTradeMoney(client.Player, flatMoney);
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                if (obj.ReceiveMoney(client.Player, flatMoney))
                {
                    client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                    return;
                }

                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                return;
            }

            client.Out.SendInventoryItemsUpdate(null);
        }

        private static void DropItem(GameClient client, eInventorySlot fromClientSlot)
        {
            DbInventoryItem item = client.Player.Inventory.GetItem(fromClientSlot);

            if (item == null)
            {
                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                client.Out.SendMessage($"Invalid item (slot #{fromClientSlot}).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (fromClientSlot < eInventorySlot.FirstBackpack)
            {
                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                return;
            }

            if (!item.IsDropable)
            {
                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                client.Out.SendMessage("You can not drop this item!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (client.Player.DropItem(fromClientSlot))
            {
                client.Out.SendMessage($"You drop {item.GetName(0, false)} on the ground!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            client.Out.SendInventoryItemsUpdate(null);
        }

        private static void EquipItemFromInventory(GameClient client, eInventorySlot fromClientSlot, ushort itemCount)
        {
            DbInventoryItem item = client.Player.Inventory.GetItem(fromClientSlot);

            if (item == null)
                return;

            eInventorySlot toClientSlot = eInventorySlot.Invalid;

            if ((eInventorySlot) item.Item_Type is >= eInventorySlot.MinEquipable and <= eInventorySlot.MaxEquipable)
                toClientSlot = (eInventorySlot) item.Item_Type;

            if (toClientSlot is eInventorySlot.Invalid)
            {
                client.Out.SendInventorySlotsUpdate([(int) fromClientSlot]);
                return;
            }

            if (toClientSlot is eInventorySlot.LeftBracer or eInventorySlot.RightBracer)
                toClientSlot = client.Player.Inventory.GetItem(eInventorySlot.LeftBracer) == null ? eInventorySlot.LeftBracer : eInventorySlot.RightBracer;
            else if (toClientSlot is eInventorySlot.LeftRing or eInventorySlot.RightRing)
                toClientSlot = client.Player.Inventory.GetItem(eInventorySlot.LeftRing) == null ? eInventorySlot.LeftRing : eInventorySlot.RightRing;

            client.Player.Inventory.MoveItem(fromClientSlot, toClientSlot, itemCount);
            return;
        }
    }
}
