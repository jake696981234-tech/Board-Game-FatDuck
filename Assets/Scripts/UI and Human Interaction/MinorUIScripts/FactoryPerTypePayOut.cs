using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FactoryPerTypePayOut : MonoBehaviour
{
    public TMP_Text pieceTypeName;
    public TMP_Text PiecePayOut;

    public void SetValues(string pieceName, float Amount)
    {
        pieceTypeName.text = pieceName;
        PiecePayOut.text = $"{Amount}";
    }
}
