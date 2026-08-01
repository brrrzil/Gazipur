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
        firstMother,
        firstEnterRichZone
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
        wireCut,
        build
    }
    public enum UISound
    {
        buy,
        sell,
        questComplete,
        buttonClick,
        openPanel,
        // Round 72: hover sound for any UI button the cursor enters.
        // ButtonAnimation.OnPointerEnter calls
        // Sounds.UIPlay(UISound.buttonHover). The matching AudioClip
        // is bound by the user in GameManager.prefab -> Sounds._uiSound
        // (one UISoundData entry with sound=buttonHover, clip=<their
        // hover wav/ogg>).
        buttonHover
    }
    public enum Quests
    {
        healMother,
        filter
    }
}