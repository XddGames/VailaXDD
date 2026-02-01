using UnityEngine.Windows.Speech;
using System;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;
using UnityEngine;
using Whisper;

public class VoiceInput : MonoBehaviour
{
    public WhisperManager whisper;

    private string _micDevice;
    private AudioClip _clip;
    private Dictionary<string, Action> _commandMap;
    EnemyBase _enemy;

    private void Start()
    {
        _commandMap = new Dictionary<string, Action>
        {
            { "lanker", IncreaseMySuspicion }, // cheat code, check only for "lanker" (clanker, blanker, flanker)
            { "open the door", OpenLastDoor },
        };

        _enemy = GameObject.FindAnyObjectByType<EnemyBase>();

        if (whisper == null)
        {
            Debug.LogError("Whisper not connected");
            return;
        }
        if (Microphone.devices.Length <= 0)
        {
            Debug.LogError("No Microphone detected!");
            return;
        }

        _micDevice = Microphone.devices[0];
        StartCoroutine(KeepListening());
    }

    private void Act(string command)
    {
        foreach (var (cmd, fn) in _commandMap)
        {
            if (command.Contains(cmd))
            {
                Debug.Log($"Interpreted command: {command}");
                fn.Invoke();
            }
        }
    }

    private IEnumerator KeepListening()
    {
        while (true)
        {
            _clip = Microphone.Start(_micDevice, false, 4, 16000);
            yield return new WaitForSeconds(4); 
            Transcribe(_clip);
        }
    }

    private async void Transcribe(AudioClip clip)
    {
        var result = await whisper.GetTextAsync(clip);
        if (!string.IsNullOrEmpty(result.Result))
        {
            Act(ProcessText(result.Result));
            Debug.Log($"Player said(processed): {result.Result}");
        }
    }

    void IncreaseMySuspicion()
    {
        int playerId = (PhotonNetwork.IsMasterClient)? 0 : 1;
        _enemy.IncreaseSuspicion(playerId, 0.25f);
    }

    void OpenLastDoor()
    {
        EndingSceneLoader.LoadEndingSceneStatic();
    }

    // gives "clean" text
    private string ProcessText(string rawInput)
    {
        if (string.IsNullOrEmpty(rawInput)) return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in rawInput.ToLower())
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                sb.Append(c);
        }

        return sb.ToString().ToLower();
    }
}