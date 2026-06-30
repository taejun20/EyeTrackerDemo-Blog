using System.Globalization;
using System.Text;
using Fove;
using Fove.Unity;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class FoveGazeDemoController : MonoBehaviour
{
    private const string ControllerName = "FOVE Gaze Demo Controller";
    private const float TargetDistance = 5.0f;
    private const float RayLength = 10.0f;
    private static readonly Vector3 LabelOffset = new Vector3(0.0f, 1.7f, 0.08f);

    [SerializeField] private Color idleColor = new Color(0.86f, 0.86f, 0.86f, 1.0f);
    [SerializeField] private Color gazedColor = new Color(0.0f, 0.95f, 0.25f, 1.0f);
    [SerializeField] private Color wallColor = new Color(0.1f, 0.105f, 0.11f, 1.0f);

    private readonly GameObject[] targets = new GameObject[3];
    private readonly Renderer[] targetRenderers = new Renderer[3];
    private readonly StringBuilder infoBuilder = new StringBuilder(1024);

    private FoveInterface foveInterface;
    private LineRenderer gazeLine;
    private Text infoText;
    private Transform cameraTransform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<FoveGazeDemoController>() != null)
            return;

        new GameObject(ControllerName).AddComponent<FoveGazeDemoController>();
    }

    private void Awake()
    {
        RegisterFoveCapabilities();
        ResolveFoveInterface();
        CreateBackgroundWall();
        CreateTargets();
        CreateGazeLine();
        CreateInfoLabel();
    }

    private void OnDestroy()
    {
        FoveManager.UnregisterCapabilities(
            ClientCapabilities.EyeTracking |
            ClientCapabilities.GazedObjectDetection |
            ClientCapabilities.UserIPD |
            ClientCapabilities.EyeBlink);
    }

    private void LateUpdate()
    {
        ResolveFoveInterface();
        UpdateTargetsFromGaze();
        UpdateGazeLine();
        UpdateInfoLabel();
        FaceLabelTowardCamera();
    }

    private static void RegisterFoveCapabilities()
    {
        FoveManager.RegisterCapabilities(
            ClientCapabilities.EyeTracking |
            ClientCapabilities.GazedObjectDetection |
            ClientCapabilities.UserIPD |
            ClientCapabilities.EyeBlink);
    }

    private void ResolveFoveInterface()
    {
        if (foveInterface != null)
            return;

        foveInterface = FindObjectOfType<FoveInterface>();
        cameraTransform = foveInterface != null ? foveInterface.transform : Camera.main != null ? Camera.main.transform : null;
    }

    private void CreateTargets()
    {
        var positions = new[]
        {
            new Vector3(-2.25f, 1.85f, TargetDistance),
            new Vector3(0.0f, 1.85f, TargetDistance),
            new Vector3(2.25f, 1.85f, TargetDistance),
        };

        var names = new[] { "Gaze Target Left", "Gaze Target Center", "Gaze Target Right" };

        for (var i = 0; i < targets.Length; i++)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = names[i];
            target.transform.SetParent(transform, false);
            target.transform.localPosition = positions[i];
            target.transform.localScale = Vector3.one * 1.05f;
            target.AddComponent<Fove.Unity.GazableObject>();

            var renderer = target.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = idleColor;

            targets[i] = target;
            targetRenderers[i] = renderer;
        }
    }

    private void CreateBackgroundWall()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wall.name = "Matte Background Wall";
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = new Vector3(0.0f, 0.9f, TargetDistance + 1.2f);
        wall.transform.localScale = new Vector3(1150.0f, 700.0f, 1.0f);
        wall.transform.rotation = Quaternion.identity;

        var collider = wall.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        var renderer = wall.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Unlit/Color"));
        renderer.material.color = wallColor;
    }

    private void CreateGazeLine()
    {
        var lineObject = new GameObject("Averaged Gaze Ray");
        lineObject.transform.SetParent(transform, false);

        gazeLine = lineObject.AddComponent<LineRenderer>();
        gazeLine.positionCount = 2;
        gazeLine.useWorldSpace = true;
        gazeLine.startWidth = 0.018f;
        gazeLine.endWidth = 0.018f;
        gazeLine.material = new Material(Shader.Find("Unlit/Color"));
        gazeLine.material.color = new Color(0.1f, 0.8f, 1.0f, 0.9f);
    }

    private void CreateInfoLabel()
    {
        var canvasObject = new GameObject("FOVE Live Data Label");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0.0f, 1.85f, TargetDistance) + LabelOffset;

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20.0f;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2600.0f, 1800.0f);
        rect.localScale = Vector3.one * 0.012f;

        var textObject = new GameObject("Live Values");
        textObject.transform.SetParent(canvasObject.transform, false);
        infoText = textObject.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        infoText.fontSize = 34;
        infoText.lineSpacing = 1.12f;
        infoText.alignment = TextAnchor.MiddleCenter;
        infoText.color = Color.white;
        infoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void UpdateTargetsFromGaze()
    {
        var gazedObject = FoveManager.GetGazedObject();
        var gazedGameObject = gazedObject.IsValid ? gazedObject.value : null;

        for (var i = 0; i < targetRenderers.Length; i++)
        {
            var isGazed = gazedGameObject == targets[i];
            targetRenderers[i].material.color = isGazed ? gazedColor : idleColor;
        }
    }

    private void UpdateGazeLine()
    {
        if (gazeLine == null || foveInterface == null)
            return;

        var ray = foveInterface.GetCombinedGazeRay();
        gazeLine.enabled = ray.IsValid;

        if (!ray.IsValid)
            return;

        gazeLine.SetPosition(0, ray.value.origin);
        gazeLine.SetPosition(1, ray.value.origin + ray.value.direction.normalized * RayLength);
    }

    private void UpdateInfoLabel()
    {
        if (infoText == null)
            return;

        var leftBlink = FoveManager.IsEyeBlinking(Eye.Left);
        var rightBlink = FoveManager.IsEyeBlinking(Eye.Right);
        infoBuilder.Length = 0;
        infoBuilder.AppendLine("FOVE live data");
        if (foveInterface != null)
            AppendRay("Gaze center ray", foveInterface.GetCombinedGazeRay());
        else
            infoBuilder.AppendLine("Gaze center ray: no FoveInterface");
        AppendBool("Left blinking", leftBlink);
        AppendBool("Right blinking", rightBlink);

        infoText.text = infoBuilder.ToString();
    }

    private void FaceLabelTowardCamera()
    {
        if (infoText == null || cameraTransform == null)
            return;

        var labelTransform = infoText.canvas.transform;
        labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - cameraTransform.position, Vector3.up);
    }

    private void AppendRay(string label, Result<Ray> result)
    {
        infoBuilder.Append(label).Append(": ");
        if (result.IsValid)
        {
            var ray = result.value;
            infoBuilder.AppendLine();
            infoBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                "origin({0:0.00}, {1:0.00}, {2:0.00}) & direction({3:0.000}, {4:0.000}, {5:0.000})",
                ray.origin.x,
                ray.origin.y,
                ray.origin.z,
                ray.direction.x,
                ray.direction.y,
                ray.direction.z);
        }
        else
        {
            infoBuilder.Append(result.error);
        }
        infoBuilder.AppendLine();
    }

    private void AppendBool(string label, Result<bool> result)
    {
        infoBuilder.Append(label).Append(": ");
        infoBuilder.Append(result.IsValid ? result.value.ToString() : result.error.ToString());
        infoBuilder.AppendLine();
    }

    private void AppendBoolPair(string label, Result<bool> left, Result<bool> right)
    {
        infoBuilder.Append(label).Append(": ");
        infoBuilder.Append(left.IsValid ? left.value.ToString() : left.error.ToString());
        infoBuilder.Append(" / ");
        infoBuilder.Append(right.IsValid ? right.value.ToString() : right.error.ToString());
        infoBuilder.AppendLine();
    }

    private void AppendInt(string label, Result<int> result)
    {
        infoBuilder.Append(label).Append(": ");
        infoBuilder.Append(result.IsValid ? result.value.ToString(CultureInfo.InvariantCulture) : result.error.ToString());
        infoBuilder.AppendLine();
    }

    private void AppendIntPair(string label, Result<int> left, Result<int> right)
    {
        infoBuilder.Append(label).Append(": ");
        infoBuilder.Append(left.IsValid ? left.value.ToString(CultureInfo.InvariantCulture) : left.error.ToString());
        infoBuilder.Append(" / ");
        infoBuilder.Append(right.IsValid ? right.value.ToString(CultureInfo.InvariantCulture) : right.error.ToString());
        infoBuilder.AppendLine();
    }

    private void AppendGazedObject(Result<GameObject> result)
    {
        infoBuilder.Append("Gazed object: ");
        if (result.IsValid)
            infoBuilder.Append(result.value != null ? result.value.name : "none");
        else
            infoBuilder.Append(result.error);
        infoBuilder.AppendLine();
    }
}
