require 'runtime.levels.Orxon.HovenInfobot'

Orxon = class("Orxon", Level)

function Orxon:initialize(internalEntity)
    Level.initialize(self, internalEntity)

    self:LoadHybrids()
end

function Orxon:LoadHybrids()
    self.hovenInfobot = HovenInfobot(self, 256)
end