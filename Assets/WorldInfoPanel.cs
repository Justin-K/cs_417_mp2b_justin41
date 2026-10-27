using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attach to a prefab whose root is a World Space Canvas.
// The prefab needs a TextMeshProUGUI child, and an optional UI Outline
// on its background Image for tutorial pop-ups.
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class WorldInfoPanel : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Outline tutorialOutline;

    [Header("Display")]
    [SerializeField] private bool faceCamera = true;

    private Transform followTarget;
    private Vector3 followOffset;

    private void Awake()
    {
        // Allow the fields to be assigned in the Inspector OR found in the prefab.
        if (messageText == null)
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (tutorialOutline == null)
            tutorialOutline = GetComponentInChildren<Outline>(true);
    }

    // Display centered at a fixed world-space position (for example, a SoilPlot).
    // lifetime <= 0 means the panel remains until Close() is called.
    public static WorldInfoPanel Spawn(
        WorldInfoPanel prefab, Vector3 center, string message,
        bool tutorial = false, float lifetime = 0f)
    {
        if (prefab == null)
        {
            Debug.LogError("WorldInfoPanel.Spawn needs a panel prefab.");
            return null;
        }

        WorldInfoPanel panel = Instantiate(prefab, center, Quaternion.identity);
        panel.gameObject.SetActive(true);

        // An inactive prefab may not have executed Awake when instantiated.
        if (panel.messageText == null)
            panel.messageText = panel.GetComponentInChildren<TextMeshProUGUI>(true);
        if (panel.tutorialOutline == null)
            panel.tutorialOutline = panel.GetComponentInChildren<Outline>(true);

        if (panel.messageText == null)
        {
            Debug.LogError("Panel prefab must contain TextMeshProUGUI.", panel);
            Destroy(panel.gameObject);
            return null;
        }

        panel.SetMessage(message);
        panel.SetTutorial(tutorial);

        if (lifetime > 0f)
            Destroy(panel.gameObject, lifetime);

        return panel;
    }

    // Display at a reference Transform and continue following it if it moves.
    public static WorldInfoPanel Spawn(
        WorldInfoPanel prefab, Transform reference, Vector3 offset,
        string message, bool tutorial = false, float lifetime = 0f)
    {
        if (reference == null)
        {
            Debug.LogWarning("WorldInfoPanel.Spawn needs a reference Transform.");
            return null;
        }

        WorldInfoPanel panel = Spawn(
            prefab, reference.position + offset, message, tutorial, lifetime);

        if (panel != null)
        {
            panel.followTarget = reference;
            panel.followOffset = offset;
        }

        return panel;
    }

    public void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message ?? string.Empty;
    }

    public void SetTutorial(bool tutorial)
    {
        if (tutorialOutline != null)
            tutorialOutline.enabled = tutorial;
        else if (tutorial)
            Debug.LogWarning("Tutorial panel needs an Outline on its background Image.", this);
    }

    private void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + followOffset;

        // A world-space panel stays legible as the camera / VR headset moves.
        if (faceCamera)
        {
            Camera viewer = Camera.main;
            if (viewer != null)
                transform.rotation = viewer.transform.rotation;
        }
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}
