using UnityEngine;
using UnityEngine.Windows.Speech;
using System;
using System.Collections.Generic;
using Photon.Pun;

public class SpeechHandler : MonoBehaviour
{
    private Dictionary<string, Action> commandMap;
    private DictationRecognizer m_DictationRecognizer;
    EnemyBase enemy;

    void Start()
    {
        enemy = GameObject.FindAnyObjectByType<EnemyBase>();
        commandMap = new Dictionary<string, Action>
        {
            { "Clanker", IncreaseMySuspicion },
            { "Open the door", OpenLastDoorEasterEgg },
        };

        m_DictationRecognizer = new DictationRecognizer();
        m_DictationRecognizer.DictationResult += (text, confidence) =>
        {
            Debug.Log($"Player Said {text}");
            // Check to see if player said something that is a command
            if (commandMap.ContainsKey(text))
            {
                Debug.Log($"Player Said {text}. Command Exists");
                commandMap[text].Invoke();
            }
        };

        m_DictationRecognizer.Start();
    }

    void IncreaseMySuspicion()
    {
        int playerId = (PhotonNetwork.IsMasterClient)? 0 : 1;
        enemy.IncreaseSuspicion(playerId, 0.25f);
    }

    void OpenLastDoorEasterEgg()
    {

    }
}