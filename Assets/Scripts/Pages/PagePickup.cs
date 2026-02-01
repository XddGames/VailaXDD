using UnityEngine;
using Photon.Pun; // Needed if you want to destroy it across the network

public class PagePickup : MonoBehaviourPun
{
    [Tooltip("1 = Top, 2 = Mid, 3 = Bottom")]
    public int pieceID; 

    // Public method called by PlayerController
    public void OnPickedUp()
    {
        if (PageManager.Instance != null)
            PageManager.Instance.CollectPiece(pieceID);

        if (PhotonNetwork.IsConnected && photonView != null && photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject); 
    }
}