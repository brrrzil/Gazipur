using UnityEngine;

// Interactable for the wire mesh fence that the player cuts
// open with a cutter tool. The Intearct(true) plays the
// Cutter animation on the UpperBody Animator layer (via the
// inherited _playerAnimTrigger = "Cutter" + PlayInteractAnimation
// helper) and locks the player movement for _animDuration
// seconds so the animation is not cancelled by walking. Holding
// E does not extend the cut; the animation runs once per press.
// Intearct(false) stops the animation if the player releases E
// early (eg an interrupting hazard). Tooltipe is shown via
// _tooltipeText if set in the Inspector; the inherited Outline
// component is enabled/disabled by InteractObject.Select().
//
// Editor setup: drop this component on the fence GameObject
// (the one with the mesh). Set the _tooltipeText (e.g. "Press
// E to cut") and optionally a _playerAnimTrigger override
// (default "Cutter") and _animDuration (default 1 second). Add
// a collider + Outline for selection detection; the rest is
// inherited from InteractObject.
public class CutterInteract : InteractObject
{
    public override void Intearct(bool isDown)
    {
        if (isDown) PlayInteractAnimation();
        else StopInteractAnimation();
    }
}
