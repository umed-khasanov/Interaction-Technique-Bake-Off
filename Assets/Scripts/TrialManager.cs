using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Highlights a random target cube each trial, logs each placement to CSV,
// resets the cube, and ends the block after the set number of trials.
public class TrialManager : MonoBehaviour
{
    [Header("Scene references")]
    public XRSocketInteractor socket;
    public XRGrabInteractable[] cubes;      // nine cubes in arc order (1..9)
    public Material targetMaterial;         // orange
    public Material distractorMaterial;     // pale blue
    public GameObject endPanel;

    [Header("Settings")]
    public string variant = "A_Ray";        // A_Ray, B_Direct or C_GazePinch
    public int trialsPerBlock = 10;
    public string menuScene = "MainMenu";

    private Vector3[] startPositions;
    private Quaternion[] startRotations;
    private int targetIndex = -1;
    private int trial = 0;
    private float trialStartTime;
    private bool blockFinished;
    private bool resetting;

    private const string FileName = "trials.csv";
    private const string DownloadsName = "BakeOff_trials.csv";

    private string LocalPath => Path.Combine(Application.persistentDataPath, FileName);

    private void Start()
    {
        Debug.Log("Trial data path: " + LocalPath);

        startPositions = new Vector3[cubes.Length];
        startRotations = new Quaternion[cubes.Length];
        for (int i = 0; i < cubes.Length; i++)
        {
            startPositions[i] = cubes[i].transform.position;
            startRotations[i] = cubes[i].transform.rotation;
        }

        if (endPanel != null) endPanel.SetActive(false);
        EnsureHeader(LocalPath);

        socket.selectEntered.AddListener(OnPlaced);
        NextTrial();
    }

    private void OnDestroy()
    {
        if (socket != null) socket.selectEntered.RemoveListener(OnPlaced);
    }

    private void NextTrial()
    {
        if (trial >= trialsPerBlock)
        {
            EndBlock();
            return;
        }

        trial++;

        int previous = targetIndex;
        do { targetIndex = Random.Range(0, cubes.Length); }
        while (targetIndex == previous && cubes.Length > 1);

        for (int i = 0; i < cubes.Length; i++)
        {
            var rend = cubes[i].GetComponentInChildren<Renderer>();
            if (rend != null)
                rend.sharedMaterial = (i == targetIndex) ? targetMaterial : distractorMaterial;
        }

        trialStartTime = Time.time;
    }

    private void OnPlaced(SelectEnterEventArgs args)
    {
        if (blockFinished || resetting) return;

        var placed = args.interactableObject as XRGrabInteractable;
        if (placed == null) return;

        int placedIndex = System.Array.IndexOf(cubes, placed);
        if (placedIndex < 0) return;

        float time = Time.time - trialStartTime;
        int error = (placedIndex == targetIndex) ? 0 : 1;

        string row = string.Join(",",
            Session.ParticipantId,
            variant,
            trial.ToString(),
            (targetIndex + 1).ToString(),
            (placedIndex + 1).ToString(),
            time.ToString("F3", CultureInfo.InvariantCulture),
            error.ToString());

        AppendRow(row);
        StartCoroutine(ResetCubeAndContinue(placedIndex));
    }

    private System.Collections.IEnumerator ResetCubeAndContinue(int index)
    {
        resetting = true;
        var cube = cubes[index];

        // Turn the socket off so it cannot grab the cube again while we reset it.
        socket.socketActive = false;
        yield return null; // let the select event finish

        if (socket.interactionManager != null && socket.IsSelecting(cube))
            socket.interactionManager.SelectExit((IXRSelectInteractor)socket, (IXRSelectInteractable)cube);

        yield return null;

        // Move the cube back home (through the Rigidbody too, so physics does not override it).
        var rb = cube.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = startPositions[index];
            rb.rotation = startRotations[index];
        }
        cube.transform.SetPositionAndRotation(startPositions[index], startRotations[index]);
        Physics.SyncTransforms();

        yield return new WaitForSeconds(0.2f);

        socket.socketActive = true;
        resetting = false;
        NextTrial();
    }

    private void EndBlock()
    {
        blockFinished = true;
        if (endPanel != null) endPanel.SetActive(true);
    }

    // Wired to the "Back to Main Menu" button.
    public void BackToMenu()
    {
        SceneManager.LoadScene(menuScene);
    }

    // ---------- CSV helpers ----------

    private const string Header = "participant,variant,trial,target,placed,time_s,error";

    private void EnsureHeader(string path)
    {
        try
        {
            if (!File.Exists(path)) File.WriteAllText(path, Header + "\n");
        }
        catch (System.Exception e) { Debug.LogWarning("CSV header failed: " + e.Message); }
    }

    private void AppendRow(string row)
    {
        try { File.AppendAllText(LocalPath, row + "\n"); }
        catch (System.Exception e) { Debug.LogWarning("CSV write failed: " + e.Message); }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            string downloads = "/storage/emulated/0/Download/" + DownloadsName;
            if (!File.Exists(downloads)) File.WriteAllText(downloads, Header + "\n");
            File.AppendAllText(downloads, row + "\n");
        }
        catch (System.Exception e) { Debug.LogWarning("Downloads copy failed: " + e.Message); }
#endif
    }
}