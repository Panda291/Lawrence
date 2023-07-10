﻿using System;
using System.Collections.Generic;
using NLua;

namespace Lawrence
{
    /// <summary>
    /// Networked Player entity. Communicates with Client to send and receive updates in the Player's game. 
    /// </summary>
    public partial class Player : Moby {
        private readonly Client _client;

        public GameState GameState = 0;

        public override float x {
            get { return _x; }
            set { base.x = value; _client.SendPacket(Packet.MakeSetPositionPacket(0, value)); }
        }

        public override float y {
            get { return _y; }
            set { base.y = value; _client.SendPacket(Packet.MakeSetPositionPacket(1, value)); }
        }

        public override float z {
            get { return _z; }
            set { base.z = value; _client.SendPacket(Packet.MakeSetPositionPacket(2, value)); }
        }

        private ushort _state = 0;
        public ushort state {
            get { return _state; }
            set { _state = value; _client.SendPacket(Packet.MakeSetPlayerStatePacket(value)); }
        }

        // Some animations can cause crashes in other games, we filter those for the time being. 
        private List<int> _filteredAnimationIDs = new List<int> {
            130  // Gold bolt collect animation
        };

        public override int animationID {
            get => _animationID;
            set {
                if (_animationID != value && !_filteredAnimationIDs.Contains(value)) {
                    _animationID = value;
                    HasChanged = true;
                }
            }
        }

        public Player(Client client) {
            _client = client;
            _client.SetHandler(this);

            InitializeInteralLuaEntity();
            
            Game.Shared().NotificationCenter().Subscribe<PostTickNotification>(OnPostTick);
        }

        public override void Delete() {
            foreach (Label label in _labels) {
                if (label != null) {
                    label.Delete();
                }
            }

            base.Delete();
            
            Game.Shared().NotificationCenter().Unsubscribe<PostTickNotification>(OnPostTick);
        }
    }

    #region Lua
    partial class Player { 
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void InitializeInteralLuaEntity() {
            object player = Game.Shared().State()["Player"];

            if (!(player is LuaTable)) {
                throw new Exception("Unable to create Player entity in Lua. `Player` is nil or not a LuaTable.");
            }

            if (!(((LuaTable)player)["new"] is LuaFunction)) {
                throw new Exception("Could not initialize new `Player` entity as initialize isn't a function on `Player` table");
            }

            LuaFunction initializeFunction = ((LuaFunction)((LuaTable)player)["new"]);

            object[] entity = initializeFunction.Call( new[] { player, this });

            if (entity.Length <= 0 || !(entity[0] is LuaTable)) {
                throw new Exception("Failed to initialize `Player` Lua entity. `Player` is not a Lua table");
            }

            SetLuaEntity((LuaTable)entity[0]);
        }
    }
    #endregion
    
    #region Gameplay related function
    partial class Player {
        public void LoadLevel(string level) {
            _level = Universe().GetLevelByName(level);
            _level.Add(this);

            SendPacket(Packet.MakeGoToPlanetPacket(_level.GetGameID()));
        }

        public void GiveItem(ushort item) {
            SendPacket(Packet.MakeSetItemPacket(item, true));
        }

        public void SetRespawn(float x, float y, float z, float rotationZ) {
            SendPacket(Packet.MakeSetRespawnPacket(x, y, z, rotationZ));
        }
    }
    #endregion

    #region Notification
    partial class Player { 
        public override void OnTick(TickNotification notification) {
            _client.Tick();

            base.OnTick(notification);
        }

        public void OnPostTick(PostTickNotification notification) {
            // Check if this client is still alive
            if (_client.IsDisconnected()) {
                Delete();
                
                return;
            }

            if (_client.GetInactiveSeconds() > Lawrence.CLIENT_INACTIVE_TIMEOUT_SECONDS) {
                Logger.Log($"Client {_client.ID} inactive for more than {Lawrence.CLIENT_INACTIVE_TIMEOUT_SECONDS} seconds.");

                // Notify client and delete client's mobys and their children
                _client.Disconnect();

                return;
            }

            // Nothing here for the player if they're not in a level yet.
            if (_level == null) {
                return;
            }

            UpdateLabels();
            
            // Find the nearest parent that masks visibility or use the level to update the client on surrounding,
            //  visible mobys.
            Entity visibilityGroup = this;

            do {
                if (visibilityGroup.MasksVisibility()) {
                    break;
                }

                if (visibilityGroup.Parent() == null) {
                    visibilityGroup = _level;
                    break;
                }

                visibilityGroup = visibilityGroup.Parent();
            } while (!visibilityGroup.MasksVisibility());

            foreach (Moby moby in visibilityGroup.Find<Moby>()) {
                if (!moby.HasChanged || (moby.IsInstanced() && !moby.HasParent(this))) {
                    continue;
                }

                float mobyDistance = DistanceTo(moby);
                if (mobyDistance > 10) {
                    if (Game.Shared().Ticks() % (int)(Math.Max(1, mobyDistance / 10)) != 0) {
                        continue;
                    }
                }
                
                Update(moby);
            }
        }

        public override void OnDeleteEntity(DeleteEntityNotification notification) {
            if (notification.Entity is Moby moby) {
                if (_client.HasMoby(moby)) {
                    _client.DeleteMoby(moby);
                }
            }

            base.OnDeleteEntity(notification);
        }
    }
    #endregion

    #region Networking
    partial class Player {
        public void Update(Moby moby) {
            // We don't update ourselves.
            if (moby == this) {
                return;
            }
            
            _client.UpdateMoby(moby);
        }

        /// <summary>
        /// Send a packet to the Player
        /// </summary>
        /// <important>
        /// This override must not call its base function. That could cause an infinite loop. 
        /// </important>
        /// <param name="packet"></param>
        public override void SendPacket((MPPacketHeader, byte[]) packet)
        {
            _client.SendPacket(packet);
        }
    }
    #endregion

    #region Client events
    partial class Player : IClientHandler
    {
        public void Collision(Moby collider, Moby collidee, bool aggressive = false)
        {
            if (collider == null) {
                throw new InvalidOperationException($"Player [{_client.GetEndpoint()}]: Got null-collider");
            }

            if (collidee == null) {
                return;
            }
            
            if (collider != this) {
                throw new InvalidOperationException($"Player [{_client.GetEndpoint()}]: Illegal collider for collision update");
            }

            if (collider == collidee) {
                throw new InvalidOperationException($"$Player [{_client.GetEndpoint()}]: Collider and collidee can't be the same entity. You can't collide with yourself.");
            }

            if (!aggressive) {
                collider.AddCollider(collidee);
                collidee.AddCollider(collider);
            }
            else {
                collidee.OnHit(collider);
                collider.OnAttack(collidee);
            }
        }

        public void ControllerInputHeld(ControllerInput input)
        {
            CallLuaFunction("OnControllerInputHeld", LuaEntity(), (int)input);
        }

        public void ControllerInputReleased(ControllerInput input)
        {
            CallLuaFunction("OnControllerInputReleased", LuaEntity(), (int)input);
        }

        public void ControllerInputTapped(ControllerInput input)
        {
            CallLuaFunction("OnControllerInputTapped", LuaEntity(), (int)input);
        }

        public uint CreateMoby()
        {
            throw new NotImplementedException();
        }

        public void UpdateMoby(MPPacketMobyUpdate mobyUpdate)
        {
            // Update this moby if 0, update child moby if not 0
            if (mobyUpdate.uuid == 0) {
                this.SetActive((mobyUpdate.mpFlags & MPMobyFlags.MP_MOBY_FLAG_ACTIVE) > 0);

                if (mobyUpdate.x != _x || mobyUpdate.y != _y || mobyUpdate.z != _z) {
                    HasChanged = true;
                }
                
                this._x = mobyUpdate.x;
                this._y = mobyUpdate.y;
                this._z = mobyUpdate.z;
                this._state = mobyUpdate.state;
                this.rotX = (float)(180 / Math.PI) * mobyUpdate.rotX;
                this.rotY = (float)(180 / Math.PI) * mobyUpdate.rotY;
                this.rotZ = (float)(180 / Math.PI) * mobyUpdate.rotZ;
                this.scale = mobyUpdate.scale;
                this.alpha = mobyUpdate.alpha / 128.0f;
                this.animationID = mobyUpdate.animationID;
                this.animationDuration = mobyUpdate.animationDuration;
            } else {
                // Update child moby 
                throw new NotImplementedException("Players can't spawn child mobys yet");
            }
        }

        public Moby Moby() {
            return this;
        }

        public void PlayerRespawned() {
            CallLuaFunction("OnRespawned", LuaEntity());
        }

        public void GameStateChanged(GameState gameState) {
            this.GameState = gameState;
            CallLuaFunction("OnGameStateChanged", LuaEntity(), (int)gameState);
        }
    }
    #endregion
    
    #region User interface
    partial class Player {
        /// <summary>
        /// We keep track of the labels we've assigned a user here, to match the way we track labels in the game client.
        /// Tuple where first item is the Label object, and the second is the hash we registered last tick. We use the hash
        ///     to only send updates to the user when we know something about the Label has updated. 
        /// </summary>
        private List<Label> _labels = new ();

        /// <summary>
        /// Adds a label to the player's screen.
        /// </summary>
        /// <param name="label"></param>
        public override void AddLabel(Label label) {
            // We loop through to check that we don't have that label or a label with the same hash already.
            // This mitigates issues if someone e.g. tries to add labels every tick, which can happen and be hard to debug
            //  for beginners.
            foreach (var _label in _labels) {
                if (_label == label) {
                    return;
                }
            }

            // Register first empty index with null label and force set HasChanged to guarantee it's sent to the player next tick
            for (int i = 0; i < _labels.Count; i++) {
                if (_labels[i] == null) {
                    label.ForceSetChanged();
                    _labels[i] = label;
                    return;
                }
            }
            
            // If it didn't return above, we just allocate more space for a new label
            _labels.Add(label);
        }

        /// <summary>
        /// Removes all the labels from the player's screen.
        /// </summary>
        public void RemoveAllLabels() {
            _labels = new List<Label>();
            
            for (int i = 0; i < _labels.Count; i++) {
                RemoveLabel(_labels[i]);
            }
        }

        /// <summary>
        /// Removes the given label from the player's screen.
        /// </summary>
        /// <param name="label">Label to remove</param>
        public override void RemoveLabel(Label label) {
            for (int i = 0; i < _labels.Count; i++) {
                if (_labels[i] == null) {
                    continue;
                }
                
                if (_labels[i] == label) {
                    _labels[i] = null;
                    
                    SendPacket(Packet.MakeDeleteHUDTextPacket((ushort)i));
                }
            }
        }

        /// <summary>
        /// Sends an update to the player about labels that need updates. 
        /// </summary>
        private void UpdateLabels() {
            for (int i = 0; i < _labels.Count; i++) {
                Label label = _labels[i];

                if (label == null) {
                    continue;
                }

                // Continue if the label hasn't been updated
                // But we force resend every 1 second in case a client missed our call. 
                if (!label.HasChanged && Game.Shared().Ticks() % 60 != 0) {
                    continue;
                }

                SendPacket(Packet.MakeSetHUDTextPacket((ushort)i, label.Text(), label.X(), label.Y(), label.Color()));
            }
        }
    }
    #endregion
}

