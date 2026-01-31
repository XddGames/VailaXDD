using UnityEngine.Windows.Speech;
using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Whisper;

public class VoiceInput : MonoBehaviour
{
    public WhisperManager whisper;
    private AudioClip recording;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            recording = Microphone.Start(null, false, 10, 16000); 
            Debug.Log("Listening...");
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            Microphone.End(null);
            Transcribe();
        }
    }

    private async void Transcribe()
    {
        if (recording == null) return;

        Debug.Log("Processing...");
        
        var result = await whisper.GetTextAsync(recording);
        
        string playerText = result.Result; 
        Debug.Log($"Player said: {playerText}");

    }
}
// public class SpeechHandler : MonoBehaviour
// {
//     private Dictionary<string, Action> commandMap;
//     private DictationRecognizer m_DictationRecognizer;
//     EnemyBase enemy;

//     void Start()
//     {
//         enemy = GameObject.FindAnyObjectByType<EnemyBase>();
//         commandMap = new Dictionary<string, Action>
//         {
//             { "Robot", IncreaseMySuspicion },
//             { "Open the door", OpenLastDoorEasterEgg },
//         };

//         m_DictationRecognizer = new DictationRecognizer();
//         m_DictationRecognizer.DictationResult += (text, confidence) =>
//         {
//             Debug.Log($"Player Said {text}");
//             // Check to see if player said something that is a command
//             if (commandMap.ContainsKey(text))
//             {
//                 Debug.Log($"Player Said {text}. Command Exists");
//                 commandMap[text].Invoke();
//             }
//         };

//         m_DictationRecognizer.Start();
//     }

//     void IncreaseMySuspicion()
//     {
//         int playerId = (PhotonNetwork.IsMasterClient)? 0 : 1;
//         enemy.IncreaseSuspicion(0, 0.25f);
//     }

//     void OpenLastDoorEasterEgg()
//     {

//     }
// }