using UnityEngine;

// Visibility controller for the three tool props on the
// Player model: pliers (kusa4ki), saw (pila), screwdriver
// (otvyortka). All three are children of a parent GameObject
// called 'Tools' that lives under the Player root, and all
// three are disabled by default. The pliers are shown during
// the Cutter animation (cutting a wire fence), the saw is
// shown during the Saw animation (picking up metal garbage),
// the screwdriver is shown during the Wrench animation
// (picking up tech/electronics garbage). The tool is hidden
// when the animation finishes.
//
// The component is invoked either by:
//   1. An Animation Event on the .anim clip itself (the
//      'Cutter_Anim', 'Saw_Anim', 'Wrench_Anim' clips in
//      Assets/Animations/Isha/Isha_Legs_Hands.controller
//      and the matching .anim files). Animation Events
//      call public methods on components attached to the
//      same GameObject hierarchy - the event 'functionName'
//      field matches one of the ShowXxx methods, and the
//      event is fired by the Animator at the configured
//      frame of the .anim clip.
//   2. Direct call from C# code - eg HoleInFance could
//      call _tools.ShowPliers() in Intearct(true) and
//      _tools.HideAll() in Intearct(false), but the
//      recommended path is the Animation Event so the
//      tool visibility is exactly synchronised with the
//      animation frame (eg the pliers appear in Isha's
//      hand at the exact moment the animation shows her
//      holding them, not when the code decides to call
//      SetActive).
//
// The default state in Awake is 'all hidden' - this matches
// the Editor setup where all three child GameObjects are
// disabled by default. The ShowXxx methods enable exactly
// one tool, the HideAll method disables all three (the
// safe default at the end of any animation that does not
// have a matching HideAll event).
public class ToolsVisibility : MonoBehaviour
{
    public enum ToolType { Pliers, Saw, Screwdriver }

    [SerializeField] private GameObject _cutter;
    [SerializeField] private GameObject _saw;
    [SerializeField] private GameObject _wrench;

    private void Awake()
    {
        HideAll();
    }

    public void ShowPliers() => SetOnly(_cutter);
    public void ShowSaw() => SetOnly(_saw);
    public void ShowScrewdriver() => SetOnly(_wrench);

    public void ShowTool(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Pliers: ShowPliers(); break;
            case ToolType.Saw: ShowSaw(); break;
            case ToolType.Screwdriver: ShowScrewdriver(); break;
        }
    }

    public void HideAll()
    {
        if (_cutter != null) _cutter.SetActive(false);
        if (_saw != null) _saw.SetActive(false);
        if (_wrench != null) _wrench.SetActive(false);
    }

    private void SetOnly(GameObject tool)
    {
        if (_cutter != null && _cutter != tool) _cutter.SetActive(false);
        if (_saw != null && _saw != tool) _saw.SetActive(false);
        if (_wrench != null && _wrench != tool) _wrench.SetActive(false);
        if (tool != null) tool.SetActive(true);
    }
}
