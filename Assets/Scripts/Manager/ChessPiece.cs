using UnityEngine;

public enum PieceType
{
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

public class ChessPiece : MonoBehaviour
{
    public PieceType pieceType;

    [HideInInspector] public int x;
    [HideInInspector] public int y;
}
