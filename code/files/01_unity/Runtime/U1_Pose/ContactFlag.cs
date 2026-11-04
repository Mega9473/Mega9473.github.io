using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Research
{
    // SessyokuTriの複数Collider管理を抽出。自分と同じrootのColliderは除く。
    public sealed class ContactFlag : MonoBehaviour
    {
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        public bool IsTouching => contacts.Count > 0;
        private bool Accept(Collider other) => other != null && other.transform.root != transform.root;
        private void OnTriggerEnter(Collider other) { if (Accept(other)) contacts.Add(other); }
        private void OnTriggerStay(Collider other) { if (Accept(other)) contacts.Add(other); }
        private void OnTriggerExit(Collider other) { contacts.Remove(other); }
        // 改善: オブジェクト無効化後に接触状態を持ち越さない。
        private void OnDisable() => contacts.Clear();
    }
}
