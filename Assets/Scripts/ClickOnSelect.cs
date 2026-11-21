using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Plays the same click sound in every variant when a cube snaps into the socket.
public class ClickOnSelect : MonoBehaviour
{
    public AudioSource source;

    private void OnEnable()
    {
        var socket = GetComponent<XRSocketInteractor>();
        if (socket != null) socket.selectEntered.AddListener(_ => { if (source != null) source.Play(); });
    }
}
