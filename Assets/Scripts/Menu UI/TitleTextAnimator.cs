using UnityEngine;
using TMPro;
using System.Collections;

public class TitleTextAnimator : MonoBehaviour
{
    public float bounceHeight = 15f;
    public float bounceSpeed = 4f;
    public float colorSpeed = 2f;

    private TMP_Text text;
    private TMP_TextInfo textInfo;
    private bool animate = true;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        StartCoroutine(AnimateText());
    }

    public void StopAnimation()
    {
        animate = false;
    }

    IEnumerator AnimateText()
    {
        text.ForceMeshUpdate();
        textInfo = text.textInfo;

        while (animate)
        {
            text.ForceMeshUpdate();
            textInfo = text.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible)
                    continue;

                int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
                int vertIndex = textInfo.characterInfo[i].vertexIndex;

                Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;
                Color32[] colors = textInfo.meshInfo[matIndex].colors32;

                float offset =
                    Mathf.Sin(Time.time * bounceSpeed + i) * bounceHeight;

                Vector3 offsetVec = new Vector3(0, offset, 0);

                vertices[vertIndex + 0] += offsetVec;
                vertices[vertIndex + 1] += offsetVec;
                vertices[vertIndex + 2] += offsetVec;
                vertices[vertIndex + 3] += offsetVec;

                Color rainbow =
                    Color.HSVToRGB((Time.time * colorSpeed + i * 0.1f) % 1f, 1f, 1f);

                colors[vertIndex + 0] = rainbow;
                colors[vertIndex + 1] = rainbow;
                colors[vertIndex + 2] = rainbow;
                colors[vertIndex + 3] = rainbow;
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices =
                    textInfo.meshInfo[i].vertices;

                textInfo.meshInfo[i].mesh.colors32 =
                    textInfo.meshInfo[i].colors32;

                text.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }

            yield return null;
        }
    }
}
