using UnityEngine;
using UnityEngine.Windows.Speech;

public class SpeechHandler : MonoBehaviour
{
    private Dictionary<string, Action> commandMap;
    private DictationRecognizer m_DictationRecognizer;

    void Start()
    {
        commandMap = new Dictionary<string, Action>
        {
            { "Clanker", IncreaseMySuspicion },
            { "Open the door", OpenLastDoorEasterEgg },
        };

        m_DictationRecognizer = new DictationRecognizer();
        m_DictationRecognizer.DictationResult += (text, confidence) =>
        {
            // Check to see if player said something that is a command
            if (commandMap.ContainsKey(text))
            {
                Debug.Log($"Player Said {text}. Command Exists")
                commandMap[text].Invoke();
            }
        };

        m_DictationRecognizer.Start();
    }

    void IncreaseMySuspicion()
    {

    }

    void OpenLastDoorEasterEgg()
    {

    }
}