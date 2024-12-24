require 'CoopOptions'
require 'CoopUniverse'

Lobby = class("Lobby")

function Lobby:initialize(host, password)
    self.host = host
    self.password = password
    
    self.started = false
    
    self.players = ObservableList({})

    self.options = CoopOptions({
        password = {
            name = "Password",
            description = "Password for the lobby. Leave blank for public.",
            handler = function(self, view, item) view.passwordInput:Activate() end,
            value = password,
        },
        friendlyFire = {
            name = "Friendly fire",
            description = "When enabled lets players hurt each other with weapons and the wrench.",
            handler = function(option, view, item) option:set(not option.value) end,
            value = true,
            accessory = {"On", "Off"}
        },
        debugStart = {
            name = "Debug Start",
            description = "Starts the game with 150k bolts and all levels, weapons, and items unlocked.",
            handler = function(option, view, item) option:set(not option.value) end,
            value = false,
            accessory = {"On", "Off"}
        },
        startPlanet = {
            name = "Start Planet",
            description = "The planet to start the game on.",
            handler = function(option, view, item) option:set((option.value+1) % 19) end,
            value = 0,
            accessory = {
                "Veldin1",
                "Novalis",
                "Aridia",
                "Kerwan",
                "Eudora",
                "Rilgar",
                "BlargStation",
                "Umbris",
                "Batalia",
                "Gaspar",
                "Orxon",
                "Pokitaru",
                "Hoven",
                "GemlikStation",
                "Oltanis",
                "Quartu",
                "KaleboIII",
                "DreksFleet",
                "Veldin2",
            }
        }
    })
    
    self.optionsList = { self.options.password, self.options.friendlyFire, self.options.debugStart, self.options.startPlanet }
    
    self.readyCallbacks = {}
    
    self.bolts = 0
    self.unlockedInfobots = {}
    self.unlockedSkillpoints = {}
    self.unlockedItems = {}
    
    self.universe = CoopUniverse(self)
end

function Lobby:AddReadyCallback(callback)
    table.insert(self.readyCallbacks, callback)
end

function Lobby:Start()
    self.unlockedInfobots = {self.options.startPlanet.value}
    
    print("Starting lobby for: ")
    for i, player in ipairs(self.players) do
        print("  " .. player:Username())
        
        player:CloseView()
        player:Start()
    end

    self.started = true
end

function Lobby:Join(player)
    player = player:Make(CoopPlayer)
    
    player.lobby = self
    self.players:Add(player)
    self.universe:AddEntity(player)
    
    player:ShowView(LobbyView(player, self))
end

function Lobby:Leave(player)
    self.players:Remove(player)
    
    if player:GUID() == self.host:GUID() then
        if #self.players > 0 then
            self.host = self.players[1]
            self.host:ToastMessage("You are now the host.", 120)
        else
            lobbyUniverse:RemoveLobby(self)
        end
    end

    lobbyUniverse:AddEntity(player)
end

function Lobby:PlayerReady(player)
    player.ready = not player.ready
    
    for i, callback in ipairs(self.readyCallbacks) do
        callback(player)
    end
end

function Lobby:AllPlayersReady()
    for i, player in ipairs(self.players) do
        if not player.ready and self.host:GUID() ~= player:GUID() then
            return false
        end
    end
    
    return true
end

function Lobby:AddUnlockedItem(item_id)
    table.insert(self.unlockedItems, item_id)
end

function Lobby:AddUnlockedInfobot(infobot_id)
    table.insert(self.unlockedInfobots, infobot_id)
end

function Lobby:AddUnlockedSkillpoint(skillpoint_id)
    table.insert(self.unlockedSkillpoints, skillpoint_id)
end
