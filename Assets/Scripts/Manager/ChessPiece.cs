using UnityEngine;

public enum PieceColor
{
    Black,
    White,
    Gray
}

public enum PieceType
{
    Normal,
    Knight,
    Rook,
    Bishop,
    Queen,
    King
}

public class ChessPiece : MonoBehaviour
{
    [HideInInspector] public int x;
    [HideInInspector] public int y;

    public PieceColor pieceColor;
    public PieceType pieceType = PieceType.Normal;
}
