using UnityEngine;
using UnityEngine.Windows.Speech; // Native Namespace

public class SimpleSpeech : MonoBehaviour
{
    private DictationRecognizer m_DictationRecognizer;

    void Start()
    {
        m_DictationRecognizer = new DictationRecognizer();

        m_DictationRecognizer.DictationResult += (text, confidence) =>
        {
            Debug.Log($"User said: {text}");
        };

        m_DictationRecognizer.Start();
    }
}