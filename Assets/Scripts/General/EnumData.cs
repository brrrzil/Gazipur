using UnityEngine;

public static class EnumData 
{
    public enum GameMode
    {
        outdors,
        trade,
        inventory,
        dialog,
        craft,
        storage,
        menu,
        die,
        otherPanels,
        win
    }
    public enum ToolsType
    {
        bag,
        wrench,
        hacksaw,
        mask,
        cutter,
        glowes,
        key
    }
    public enum DialogType
    {
        start,
        startTrader,
        motherStart,
        traderAfterBuy,
        motherDisease,
        motherMedecine
    }
    public enum RemarksType
    {        
        noWrench,
        noGrowes,
        noHacksaw,
        inventoryFool,
        noCutters,
        noKey,
        noMask,
        maskReady,
        tooEarly,
        relaxMom,
        filterNeed,
        fewParts,
        foolParts,
        foundBlueprint,
        closeBlueprint,
        foundPart,
        hungry,
        thirst,
        iAdult,
        rohulSelBuy,
        rohulHelp,
        lie,
        firstMother
    }
    public enum FilterParts
    {
        shortTube,
        longTube,
        bascet,
        teleTube,
        support,
        pump,
        filter,
        solarBat,
    }
    public enum PlayerSound
    {
        eat,
        drink,
        pickedTrash,
        pickedMettal,
        pickedTecno,
        wireCut
    }
    public enum UISound
    {
        buy, 
        sell,
        questComplete,
        buttonClick,
        openPanel
    }
    public enum Quests
    {
        healMother,
        filter
    }
}
