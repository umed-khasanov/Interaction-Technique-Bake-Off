using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Used by the MenuManager object in the MainMenu scene.
public class MainMenu : MonoBehaviour
{
    public TMP_Text participantLabel;

    private void Start()
    {
        Refresh();
    }

    public void NextParticipant()
    {
        Session.ParticipantNumber++;
        Refresh();
    }

    public void PreviousParticipant()
    {
        if (Session.ParticipantNumber > 1) Session.ParticipantNumber--;
        Refresh();
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void Refresh()
    {
        if (participantLabel != null) participantLabel.text = Session.ParticipantId;
    }
}
