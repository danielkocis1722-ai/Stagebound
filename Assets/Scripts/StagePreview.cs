using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Scene-derived images keep browsing independent of gameplay scene lifecycles.
public sealed class StagePreview : MonoBehaviour
{
    readonly Dictionary<string, Texture> cache = new Dictionary<string, Texture>();
    RawImage current, outgoing;
    TMP_Text placeholder;
    StageDefinition shown;
    float blend = 1;

    RawImage Layer(string label)
    {
        var image = new GameObject(label, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(transform, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    public void Show(StageDefinition stage)
    {
        if (current == null)
        {
            current = Layer("Stage view"); outgoing = Layer("Outgoing stage view");
            placeholder = StageboundMenuUI.Text(transform, "PREVIEW UNAVAILABLE", 22, new Vector2(30, -150), new Vector2(980, 50), StageboundMenuUI.Muted);
            placeholder.alignment = TextAlignmentOptions.Center;
        }
        if (shown == stage) return;
        shown = stage;
        Texture next = null;
        if (stage != null && !cache.TryGetValue(stage.stageId, out next))
        {
            next = Resources.Load<Texture2D>("StagePreviews/" + stage.stageId);
            if (next == null && stage.previewImage != null) next = stage.previewImage.texture;
            cache[stage.stageId] = next;
        }
        outgoing.texture = current.texture;
        outgoing.uvRect = current.uvRect;
        outgoing.enabled = Application.isPlaying && current.enabled;
        outgoing.color = Color.white;
        blend = 0;
        current.texture = next;
        current.enabled = next != null;
        current.uvRect = new Rect(0, 0, 1, 1);
        placeholder.gameObject.SetActive(next == null);
        if (next != null)
        {
            float sourceAspect = (float)next.width / next.height;
            float targetAspect = 1040f / 360f;
            float h = Mathf.Min(1, sourceAspect / targetAspect);
            float w = Mathf.Min(1, targetAspect / sourceAspect);
            current.uvRect = new Rect((1 - w) * .5f, (1 - h) * .5f, w, h);
        }
    }

    void Update()
    {
        if (outgoing == null || !outgoing.enabled) return;
        blend = Mathf.Min(1, blend + Time.unscaledDeltaTime / .35f);
        outgoing.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, blend));
        if (blend >= 1) outgoing.enabled = false;
    }
}
