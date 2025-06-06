function GetAPItemType(ap_item)
    if ap_item == 48 or ap_item == 49 or ap_item == 50 or ap_item == 52 or ap_item == 53 then
        return "special"
    elseif ap_item < 100 then
        return "item"
    elseif ap_item < 300 then
        return "planet"
    else
        return "gold bolt"
    end
end

function APItemToItem(ap_item) -- assumes user verified ap_item is an "item"
    return ap_item -- ap world is already lined up with real ids
end

function APItemToSpecial(ap_item) -- assumes user verified ap_item is an "item"
    if ap_item == 48 then
        return Player.offset.has_zoomerator
    elseif ap_item == 49 then
        return Player.offset.has_raritanium
    elseif ap_item == 50 then
        return Player.offset.has_codebot
    elseif ap_item == 52 then
        return Player.offset.has_premium_nanotech
    elseif ap_item == 53 then
        return Player.offset.has_ultra_nanotech
    else
        return ap_item -- should not happen
    end
end

function APItemToPlanet(ap_item) -- assumes user verified ap_item is an "planet"
    return ap_item - 100 -- ap world is already lined up with real ids +100
end

function APItemToGoldBolt(ap_item) -- assumes user verified ap_item is an "planet"
    -- translate gold bolts back to (planet, number)
    -- check if we will even care about this
    return ap_item
end