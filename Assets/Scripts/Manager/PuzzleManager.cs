using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PuzzleManager : MonoBehaviour
{
    [Header("Background Image Rotation")]
    public Sprite[] backgroundImages;
    public Image backgroundImage;
    private int currentImageIndex = 0;
    private float timer = 0f;
    private float changeInterval = 15f;

    [Header("Button Management")]
    public Button pauseButton;
    public Button homeButton;
    public Button continueButton;
    public Button quitButton;

    [Header("Game Settings UI")]
    public GameObject winPanel;
    public GameObject losePanel;
    private bool hasGameEnded = false;
    public AudioClip loseSound;
    public AudioClip winSound;
    public TextMeshProUGUI moveCountText;
    private int moveCountLeft = 20;
    public TextMeshProUGUI levelText;
    int levelIndex = 1;


    [Header("Grid Settings")]
    public int width = 8;
    public int height = 8;
    public ChessPiece lastSwappedA;
    public ChessPiece lastSwappedB;

    public GameObject[] pawnPrefabs;
    public GameObject knightPrefab;
    public GameObject rookPrefab;
    public GameObject bishopPrefab;
    public GameObject queenPrefab;
    public GameObject kingPrefab;


    public ChessPiece[,] grid { get; private set; }

    public Transform gridOrigin;

    private float cellSize;

    [Header("Grid Positioning")]
    [Tooltip("Grid'in dikey konumu (negatif = aşağı)")]
    public float gridVerticalOffset = -2f;

    [Tooltip("Tile'lar arası boşluk çarpanı (1.0 = boşluk yok, 1.2 = %20 boşluk)")]
    [Range(1.0f, 2.0f)]
    public float spacingMultiplier = 1.15f;

    [Header("Match System")]
    public AudioClip matchSound;
    public AudioClip knightSound;
    public AudioClip rookSound;
    public AudioClip bishopSound;
    public AudioClip queenSound;
    public AudioClip kingSound;

    public AudioClip knightAttackSound;
    public ParticleSystem knightAttackEffect;


    public ParticleSystem matchEffectPrefab;

    private bool isProcessingMatches = false;
    private ChessPiece lastSwappedPiece;


    [Header("Input System")]
    private ChessPiece selectedPiece = null;
    private bool isDragging = false;
    private Vector2 dragStartPos;
    private float minDragDistance = 0.3f;


    [Header("Knight Flight Settings")]
    public float knightFlyDuration = 0.4f;
    public float knightSpawnHeight = 2.5f;


    void Start()
    {
        UpdateUI();
        InitUI();

        grid = new ChessPiece[width, height];

        CalculateCellSize();
        CenterGrid();
        CreateGrid();
    }

    void InitUI()
    {
        if (backgroundImage != null && backgroundImages.Length > 0)
            backgroundImage.sprite = backgroundImages[currentImageIndex];

        pauseButton.onClick.AddListener(PauseGame);

        homeButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        });

        continueButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1;
            pauseButton.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(false);
            homeButton.gameObject.SetActive(false);
            quitButton.gameObject.SetActive(false);
        });

        quitButton.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    void Update()
    {
        if (hasGameEnded)
            return;

        timer += Time.deltaTime;
        if (timer >= changeInterval)
        {
            ChangeBackground();
            timer = 0f;
        }

        if (!isProcessingMatches)
        {
            HandleInput();
        }
    }

    void ChangeBackground()
    {
        currentImageIndex = (currentImageIndex + 1) % backgroundImages.Length;
        backgroundImage.sprite = backgroundImages[currentImageIndex];
    }

    void PauseGame()
    {
        Time.timeScale = 0;
        pauseButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(true);
        homeButton.gameObject.SetActive(true);
        quitButton.gameObject.SetActive(true);
    }

    // ================= GRID =================

    void CalculateCellSize()
    {
        GameObject temp = Instantiate(pawnPrefabs[0]);
        SpriteRenderer sr = temp.GetComponent<SpriteRenderer>();
        cellSize = sr.sprite.bounds.size.x * temp.transform.localScale.x;
        Destroy(temp);
    }



    void CenterGrid()
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        float gridWidth = width * spacedCellSize;
        float gridHeight = height * spacedCellSize;

        gridOrigin.position = new Vector3(
            -gridWidth / 2f + spacedCellSize / 2f,
            -gridHeight / 2f + spacedCellSize / 2f + gridVerticalOffset,
            0
        );
    }

    void CreateGrid()
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpawnSafePawn(x, y, spacedCellSize);
            }
        }

        Debug.Log("[Grid] Başlangıç grid'i oluşturuldu");
        StartCoroutine(CheckAndResolveMatches());
    }

    void SpawnPawn(int x, int y, float spacedCellSize)
    {
        int randomIndex = Random.Range(0, Mathf.Min(3, pawnPrefabs.Length));

        GameObject pawn = Instantiate(pawnPrefabs[randomIndex], gridOrigin);

        pawn.transform.localPosition = new Vector3(
            x * spacedCellSize,
            y * spacedCellSize,
            0
        );

        ChessPiece piece = pawn.GetComponent<ChessPiece>();
        piece.x = x;
        piece.y = y;

        if (randomIndex == 0)
            piece.pieceColor = PieceColor.Black;
        else if (randomIndex == 1)
            piece.pieceColor = PieceColor.White;
        else
            piece.pieceColor = PieceColor.Gray;

        grid[x, y] = piece;
    }
    void SpawnSafePawn(int x, int y, float spacedCellSize)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int randomIndex = Random.Range(0, Mathf.Min(3, pawnPrefabs.Length));
            PieceColor color;

            if (randomIndex == 0)
                color = PieceColor.Black;
            else if (randomIndex == 1)
                color = PieceColor.White;
            else
                color = PieceColor.Gray;

            bool horizontalMatch = false;
            if (x >= 2)
            {
                if (grid[x - 1, y] != null && grid[x - 2, y] != null)
                {
                    if (grid[x - 1, y].pieceColor == color &&
                        grid[x - 2, y].pieceColor == color)
                    {
                        horizontalMatch = true;
                    }
                }
            }

            bool verticalMatch = false;
            if (y >= 2)
            {
                if (grid[x, y - 1] != null && grid[x, y - 2] != null)
                {
                    if (grid[x, y - 1].pieceColor == color &&
                        grid[x, y - 2].pieceColor == color)
                    {
                        verticalMatch = true;
                    }
                }
            }

            if (!horizontalMatch && !verticalMatch)
            {
                GameObject pawn = Instantiate(pawnPrefabs[randomIndex], gridOrigin);
                pawn.transform.localPosition = new Vector3(
                    x * spacedCellSize,
                    y * spacedCellSize,
                    0
                );

                ChessPiece piece = pawn.GetComponent<ChessPiece>();
                piece.x = x;
                piece.y = y;
                piece.pieceColor = color;

                grid[x, y] = piece;
                return;
            }
        }

        SpawnPawn(x, y, spacedCellSize);
    }
    void UpdateUI()
    {
        moveCountText.text = moveCountLeft.ToString();
        levelText.text = levelIndex.ToString();
    }





    // ================= MATCH SİSTEMİ =================

    List<ChessPiece> FindAllMatches()
    {
        HashSet<ChessPiece> matchSet = new HashSet<ChessPiece>();

        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                ChessPiece a = grid[x, y];
                ChessPiece b = grid[x + 1, y];
                ChessPiece c = grid[x, y + 1];
                ChessPiece d = grid[x + 1, y + 1];

                if (a != null && b != null && c != null && d != null)
                {
                    if (a.pieceType == PieceType.Normal &&
                        b.pieceType == PieceType.Normal &&
                        c.pieceType == PieceType.Normal &&
                        d.pieceType == PieceType.Normal &&
                        a.pieceColor == b.pieceColor &&
                        a.pieceColor == c.pieceColor &&
                        a.pieceColor == d.pieceColor)
                    {
                        // 2x2 KARE BULUNDU!
                        matchSet.Add(a);
                        matchSet.Add(b);
                        matchSet.Add(c);
                        matchSet.Add(d);

                        Debug.Log($"[2x2 Square] Bulundu: ({x},{y}) - Renk: {a.pieceColor}");
                    }
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                ChessPiece p1 = grid[x, y];
                ChessPiece p2 = grid[x + 1, y];
                ChessPiece p3 = grid[x + 2, y];

                if (p1 != null && p2 != null && p3 != null)
                {
                    if (p1.pieceType == PieceType.Normal &&
                        p2.pieceType == PieceType.Normal &&
                        p3.pieceType == PieceType.Normal &&
                        p1.pieceColor == p2.pieceColor &&
                        p2.pieceColor == p3.pieceColor)
                    {
                        matchSet.Add(p1);
                        matchSet.Add(p2);
                        matchSet.Add(p3);
                    }
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                ChessPiece p1 = grid[x, y];
                ChessPiece p2 = grid[x, y + 1];
                ChessPiece p3 = grid[x, y + 2];

                if (p1 != null && p2 != null && p3 != null)
                {
                    if (p1.pieceType == PieceType.Normal &&
                        p2.pieceType == PieceType.Normal &&
                        p3.pieceType == PieceType.Normal &&
                        p1.pieceColor == p2.pieceColor &&
                        p2.pieceColor == p3.pieceColor)
                    {
                        matchSet.Add(p1);
                        matchSet.Add(p2);
                        matchSet.Add(p3);
                    }
                }
            }
        }

        return new List<ChessPiece>(matchSet);
    }
    IEnumerator CheckAndResolveMatches()
    {
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            System.Collections.Generic.List<ChessPiece> matches = FindAllMatches();

            if (matches.Count == 0)
            {
                Debug.Log("[Match] Grid hazır!");
                break;
            }

            Debug.Log($"[Match] {matches.Count} eşleşme bulundu");
            yield return StartCoroutine(DestroyMatches(matches));
            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());

            yield return new WaitForSeconds(0.3f);
        }

        isProcessingMatches = false;
    }

    IEnumerator DestroyMatches(List<ChessPiece> matches)
    {
        if (lastSwappedA != null && lastSwappedA.pieceType == PieceType.Rook)
        {
            yield return StartCoroutine(ActivateRookPower(lastSwappedA));
            yield break;
        }

        if (lastSwappedB != null && lastSwappedB.pieceType == PieceType.Rook)
        {
            yield return StartCoroutine(ActivateRookPower(lastSwappedB));
            yield break;
        }


        if (matchSound != null)
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position);

        List<ChessPiece> squareMatches = Find2x2SquareInMatches(matches);
        List<ChessPiece> rookMatch = FindRookMatches();
        List<ChessPiece> bishopMatch = FindBishopMatches();
        List<ChessPiece> queenMatch = FindQueenMatches();
        List<ChessPiece> kingMatch = FindKingMatches();

        if (kingMatch.Count == 4)
        {
            ChessPiece origin = kingMatch[0];
            int kx = origin.x;
            int ky = origin.y;
            PieceColor color = origin.pieceColor;

            foreach (ChessPiece p in kingMatch)
            {
                grid[p.x, p.y] = null;
                Destroy(p.gameObject);
            }
            if (matchEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(matchEffectPrefab,
                    origin.transform.position,
                    Quaternion.identity);
                Destroy(effect.gameObject, 2f);
            }
            yield return new WaitForSeconds(0.15f);
            SpawnKing(kx, ky, color);
            yield break;
        }


        if (queenMatch.Count > 0)
        {
            ChessPiece center = queenMatch[0];
            int qx = center.x;
            int qy = center.y;
            PieceColor color = center.pieceColor;

            foreach (ChessPiece p in queenMatch)
            {
                grid[p.x, p.y] = null;
                Destroy(p.gameObject);
            }
            if (matchEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(matchEffectPrefab,
                    center.transform.position,
                    Quaternion.identity);
                Destroy(effect.gameObject, 2f);
            }
            yield return new WaitForSeconds(0.15f);
            SpawnQueen(qx, qy, color);
            yield break;
        }

        if (bishopMatch.Count == 5)
        {
            ChessPiece center = bishopMatch[2];
            int bx = center.x;
            int by = center.y;
            PieceColor color = center.pieceColor;

            foreach (ChessPiece p in bishopMatch)
            {
                grid[p.x, p.y] = null;
                Destroy(p.gameObject);
            }
            if (matchEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(matchEffectPrefab,
                    center.transform.position,
                    Quaternion.identity);
                Destroy(effect.gameObject, 2f);
            }
            yield return new WaitForSeconds(0.15f);
            SpawnBishop(bx, by, color);
            yield break;
        }

        if (rookMatch.Count == 4)
        {
            ChessPiece center = rookMatch[1];
            int rx = center.x;
            int ry = center.y;
            PieceColor color = center.pieceColor;

            foreach (ChessPiece p in rookMatch)
            {
                grid[p.x, p.y] = null;
                Destroy(p.gameObject);
            }
            if (matchEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(matchEffectPrefab,
                    center.transform.position,
                    Quaternion.identity);
                Destroy(effect.gameObject, 2f);
            }
            yield return new WaitForSeconds(0.15f);
            SpawnRook(rx, ry, color);
            yield break;
        }

        if (squareMatches.Count == 4)
        {
            Debug.Log("[2x2 Square] 4 tile yok edilip Knight oluşturuluyor!");

            // Sol alt tile'ın pozisyonunu ve rengini al
            ChessPiece bottomLeft = squareMatches[0];
            int knightX = bottomLeft.x;
            int knightY = bottomLeft.y;
            PieceColor knightColor = bottomLeft.pieceColor;

            // 4 tile'ı yok et
            foreach (ChessPiece piece in squareMatches)
            {
                if (piece != null)
                {
                    if (matchEffectPrefab != null)
                    {
                        ParticleSystem effect = Instantiate(matchEffectPrefab,
                            piece.transform.position,
                            Quaternion.identity);
                        Destroy(effect.gameObject, 2f);
                    }

                    grid[piece.x, piece.y] = null;
                    Destroy(piece.gameObject);
                }
            }

            yield return new WaitForSeconds(0.2f);

            SpawnKnight(knightX, knightY, knightColor);

            // Kalan match'leri square listesinden çıkar
            foreach (ChessPiece piece in squareMatches)
            {
                matches.Remove(piece);
            }
        }

        foreach (ChessPiece piece in matches)
        {
            if (piece == null) continue;
            if (grid[piece.x, piece.y] != piece) continue;

            if (matchEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(matchEffectPrefab,
                    piece.transform.position,
                    Quaternion.identity);
                Destroy(effect.gameObject, 2f);
            }

            grid[piece.x, piece.y] = null;
            Destroy(piece.gameObject);
        }

        yield return new WaitForSeconds(0.2f);
    }
    List<ChessPiece> Find2x2SquareInMatches(List<ChessPiece> matches)
    {
        // Match'ler içinde 2x2 kare var mı bul
        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                ChessPiece a = grid[x, y];
                ChessPiece b = grid[x + 1, y];
                ChessPiece c = grid[x, y + 1];
                ChessPiece d = grid[x + 1, y + 1];

                if (a != null && b != null && c != null && d != null)
                {
                    if (matches.Contains(a) && matches.Contains(b) &&
                        matches.Contains(c) && matches.Contains(d))
                    {
                        if (a.pieceType == PieceType.Normal &&
                            b.pieceType == PieceType.Normal &&
                            c.pieceType == PieceType.Normal &&
                            d.pieceType == PieceType.Normal &&
                            a.pieceColor == b.pieceColor &&
                            a.pieceColor == c.pieceColor &&
                            a.pieceColor == d.pieceColor)
                        {
                            // 2x2 kare bulundu!
                            return new List<ChessPiece> { a, b, c, d };
                        }
                    }
                }
            }
        }

        return new List<ChessPiece>();
    }
    IEnumerator DropPieces()
    {
        float spacedCellSize = cellSize * spacingMultiplier;
        bool pieceMoved = true;

        while (pieceMoved)
        {
            pieceMoved = false;

            for (int x = 0; x < width; x++)
            {
                for (int y = 1; y < height; y++)
                {
                    if (grid[x, y] != null && grid[x, y - 1] == null)
                    {
                        ChessPiece piece = grid[x, y];
                        grid[x, y] = null;
                        grid[x, y - 1] = piece;
                        piece.y = y - 1;

                        Vector3 targetPos = new Vector3(
                            x * spacedCellSize,
                            (y - 1) * spacedCellSize,
                            0
                        );
                        StartCoroutine(MovePiece(piece.transform, targetPos, 0.2f));

                        pieceMoved = true;
                    }
                }
            }

            if (pieceMoved)
                yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator FillEmptySpaces()
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null)
                {
                    int randomIndex = Random.Range(0, Mathf.Min(3, pawnPrefabs.Length));

                    GameObject pawn = Instantiate(pawnPrefabs[randomIndex], gridOrigin);

                    pawn.transform.localPosition = new Vector3(
                        x * spacedCellSize,
                        height * spacedCellSize,
                        0
                    );

                    ChessPiece piece = pawn.GetComponent<ChessPiece>();
                    piece.x = x;
                    piece.y = y;

                    if (randomIndex == 0)
                        piece.pieceColor = PieceColor.Black;
                    else if (randomIndex == 1)
                        piece.pieceColor = PieceColor.White;
                    else
                        piece.pieceColor = PieceColor.Gray;

                    grid[x, y] = piece;

                    Vector3 targetPos = new Vector3(
                        x * spacedCellSize,
                        y * spacedCellSize,
                        0
                    );
                    StartCoroutine(MovePiece(piece.transform, targetPos, 0.3f));
                }
            }
        }

        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator MovePiece(Transform piece, Vector3 targetPos, float duration)
    {
        Vector3 startPos = piece.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (piece == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            piece.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if (piece != null)
            piece.localPosition = targetPos;
    }



    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            Vector2 inputPos = GetInputPosition();
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(inputPos);

            Collider2D hit = Physics2D.OverlapPoint(worldPos);

            if (hit != null)
            {
                ChessPiece piece = hit.GetComponent<ChessPiece>();
                if (piece != null)
                {
                    selectedPiece = piece;
                    dragStartPos = worldPos;
                    isDragging = true;

                    Debug.Log($"[Input] Seçildi: ({piece.x}, {piece.y}) - {piece.pieceColor}");
                }
            }
        }

        if (isDragging && selectedPiece != null)
        {
            if (Input.GetMouseButton(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved))
            {
                Vector2 inputPos = GetInputPosition();
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(inputPos);
                Vector2 dragDelta = worldPos - dragStartPos;

                if (dragDelta.magnitude >= minDragDistance)
                {
                    Vector2Int direction = GetSwipeDirection(dragDelta);

                    if (direction != Vector2Int.zero)
                    {
                        int targetX = selectedPiece.x + direction.x;
                        int targetY = selectedPiece.y + direction.y;

                        if (IsValidPosition(targetX, targetY))
                        {
                            ChessPiece targetPiece = grid[targetX, targetY];

                            if (targetPiece != null)
                            {
                                Debug.Log($"[Input] Swap: ({selectedPiece.x},{selectedPiece.y}) <-> ({targetX},{targetY})");
                                StartCoroutine(TrySwap(selectedPiece, targetPiece));
                            }
                        }

                        isDragging = false;
                        selectedPiece = null;
                    }
                }
            }
        }

        if (Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended))
        {
            isDragging = false;
            selectedPiece = null;
        }
    }

    Vector2 GetInputPosition()
    {
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }
        return Input.mousePosition;
    }

    Vector2Int GetSwipeDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            return delta.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }

    bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    IEnumerator TrySwap(ChessPiece piece1, ChessPiece piece2)
    {
        if (piece1 == null || piece2 == null)
            yield break;

        isProcessingMatches = true;

        // 🔥 SON SWAP KAYDI (KALE / ÖZEL TAŞ İÇİN)
        lastSwappedA = piece1;
        lastSwappedB = piece2;

        moveCountLeft--;
        UpdateUI();

        // Grid pozisyonlarını değiştir
        int tempX = piece1.x;
        int tempY = piece1.y;

        grid[piece1.x, piece1.y] = piece2;
        grid[piece2.x, piece2.y] = piece1;

        piece1.x = piece2.x;
        piece1.y = piece2.y;
        piece2.x = tempX;
        piece2.y = tempY;

        float spacedCellSize = cellSize * spacingMultiplier;

        Vector3 piece1Target = new Vector3(piece1.x * spacedCellSize, piece1.y * spacedCellSize, 0);
        Vector3 piece2Target = new Vector3(piece2.x * spacedCellSize, piece2.y * spacedCellSize, 0);

        StartCoroutine(MovePiece(piece1.transform, piece1Target, 0.2f));
        StartCoroutine(MovePiece(piece2.transform, piece2Target, 0.2f));

        yield return new WaitForSeconds(0.25f);

        // ♞ KNIGHT + PAWN ÖZEL ETKİ
        if (
            (piece1.pieceType == PieceType.Knight && piece2.pieceType == PieceType.Normal) ||
            (piece2.pieceType == PieceType.Knight && piece1.pieceType == PieceType.Normal)
        )
        {
            grid[piece1.x, piece1.y] = null;
            grid[piece2.x, piece2.y] = null;

            Destroy(piece1.gameObject);
            Destroy(piece2.gameObject);

            yield return StartCoroutine(SpawnKnightAttackers());
            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());

            isProcessingMatches = false;
            yield break;
        }

        // Normal match kontrolü
        List<ChessPiece> matches = FindAllMatches();

        if (matches.Count > 0)
        {
            yield return StartCoroutine(DestroyMatches(matches));
            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());
        }
        else
        {
            // Geri al
            int temp2X = piece1.x;
            int temp2Y = piece1.y;

            grid[piece1.x, piece1.y] = piece2;
            grid[piece2.x, piece2.y] = piece1;

            piece1.x = piece2.x;
            piece1.y = piece2.y;
            piece2.x = temp2X;
            piece2.y = temp2Y;

            Vector3 piece1Original = new Vector3(piece1.x * spacedCellSize, piece1.y * spacedCellSize, 0);
            Vector3 piece2Original = new Vector3(piece2.x * spacedCellSize, piece2.y * spacedCellSize, 0);

            StartCoroutine(MovePiece(piece1.transform, piece1Original, 0.15f));
            StartCoroutine(MovePiece(piece2.transform, piece2Original, 0.15f));

            yield return new WaitForSeconds(0.2f);
        }

        isProcessingMatches = false;

        if (moveCountLeft <= 0 && !hasGameEnded)
        {
            hasGameEnded = true;
            AudioSource.PlayClipAtPoint(loseSound, Camera.main.transform.position);
            losePanel.SetActive(true);
        }
    }


    IEnumerator CheckAndResolveMatchesAfterSwap()
    {
        while (true)
        {
            System.Collections.Generic.List<ChessPiece> matches = FindAllMatches();

            if (matches.Count == 0)
            {
                break;
            }

            Debug.Log($"[Combo] {matches.Count} yeni eşleşme bulundu!");

            yield return StartCoroutine(DestroyMatches(matches));
            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());

            yield return new WaitForSeconds(0.3f);
        }
    }

    void SpawnKnight(int x, int y, PieceColor color)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        GameObject knight = Instantiate(knightPrefab, gridOrigin);
        AudioSource.PlayClipAtPoint(knightSound, Camera.main.transform.position);

        knight.transform.localPosition = new Vector3(
            x * spacedCellSize,
            y * spacedCellSize,
            0
        );

        ChessPiece piece = knight.GetComponent<ChessPiece>();
        piece.x = x;
        piece.y = y;
        piece.pieceColor = color;
        piece.pieceType = PieceType.Knight;

        grid[x, y] = piece;
    }



    List<ChessPiece> Find4LineMatch(int startX, int startY, Vector2Int dir)
    {
        List<ChessPiece> result = new List<ChessPiece>();

        ChessPiece first = grid[startX, startY];
        if (first == null || first.pieceType != PieceType.Normal)
            return result;

        PieceColor color = first.pieceColor;
        result.Add(first);

        for (int i = 1; i < 4; i++)
        {
            int nx = startX + dir.x * i;
            int ny = startY + dir.y * i;

            if (!IsValidPosition(nx, ny))
                return new List<ChessPiece>();

            ChessPiece next = grid[nx, ny];
            if (next == null ||
                next.pieceType != PieceType.Normal ||
                next.pieceColor != color)
                return new List<ChessPiece>();

            result.Add(next);
        }

        return result.Count == 4 ? result : new List<ChessPiece>();
    }

    List<ChessPiece> FindRookMatches()
    {
        List<ChessPiece> rookMatches = new List<ChessPiece>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null) continue;

                // YATAY 4
                var horizontal = Find4LineMatch(x, y, Vector2Int.right);
                if (horizontal.Count == 4)
                    return horizontal;

                // DİKEY 4
                var vertical = Find4LineMatch(x, y, Vector2Int.up);
                if (vertical.Count == 4)
                    return vertical;
            }
        }

        return rookMatches;
    }
    void SpawnRook(int x, int y, PieceColor color)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        GameObject rook = Instantiate(rookPrefab, gridOrigin);
        rook.name = "Rook";
        AudioSource.PlayClipAtPoint(rookSound, Camera.main.transform.position);

        rook.transform.localPosition = new Vector3(
            x * spacedCellSize,
            y * spacedCellSize,
            0
        );

        ChessPiece piece = rook.GetComponent<ChessPiece>();
        piece.x = x;
        piece.y = y;
        piece.pieceColor = color;
        piece.pieceType = PieceType.Rook;

        grid[x, y] = piece;

        Debug.Log($"[ROOK] Oluşturuldu ({x},{y}) Renk: {color}");
    }



    List<ChessPiece> Find5LineMatch(int startX, int startY, Vector2Int dir)
    {
        List<ChessPiece> result = new List<ChessPiece>();

        ChessPiece first = grid[startX, startY];
        if (first == null ||
            first.pieceType != PieceType.Normal ||
            first.pieceColor == PieceColor.Gray)
            return result;

        PieceColor color = first.pieceColor;
        result.Add(first);

        for (int i = 1; i < 5; i++)
        {
            int nx = startX + dir.x * i;
            int ny = startY + dir.y * i;

            if (!IsValidPosition(nx, ny))
                return new List<ChessPiece>();

            ChessPiece next = grid[nx, ny];
            if (next == null ||
                next.pieceType != PieceType.Normal ||
                next.pieceColor != color)
                return new List<ChessPiece>();

            result.Add(next);
        }

        return result.Count == 5 ? result : new List<ChessPiece>();
    }

    List<ChessPiece> FindBishopMatches()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null) continue;

                // YATAY 5
                var horizontal = Find5LineMatch(x, y, Vector2Int.right);
                if (horizontal.Count == 5)
                    return horizontal;

                // DİKEY 5
                var vertical = Find5LineMatch(x, y, Vector2Int.up);
                if (vertical.Count == 5)
                    return vertical;
            }
        }

        return new List<ChessPiece>();
    }
    void SpawnBishop(int x, int y, PieceColor color)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        GameObject bishop = Instantiate(bishopPrefab, gridOrigin);
        if (bishopSound != null)
            AudioSource.PlayClipAtPoint(bishopSound, Camera.main.transform.position);

        bishop.transform.localPosition = new Vector3(
            x * spacedCellSize,
            y * spacedCellSize,
            0
        );

        ChessPiece piece = bishop.GetComponent<ChessPiece>();
        piece.x = x;
        piece.y = y;
        piece.pieceColor = color;
        piece.pieceType = PieceType.Bishop;

        grid[x, y] = piece;

        Debug.Log($"[BISHOP] Oluşturuldu ({x},{y}) Renk: {color}");
    }



    List<ChessPiece> FindQueenMatches()
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                ChessPiece center = grid[x, y];
                if (center == null || center.pieceType != PieceType.Normal)
                    continue;

                PieceColor color = center.pieceColor;

                ChessPiece left = grid[x - 1, y];
                ChessPiece right = grid[x + 1, y];
                ChessPiece up = grid[x, y + 1];
                ChessPiece down = grid[x, y - 1];

                // ⊥ (T yukarı)
                if (left && right && up &&
                    left.pieceColor == color &&
                    right.pieceColor == color &&
                    up.pieceColor == color)
                {
                    return new List<ChessPiece> { center, left, right, up };
                }

                // ⊤ (T aşağı)
                if (left && right && down &&
                    left.pieceColor == color &&
                    right.pieceColor == color &&
                    down.pieceColor == color)
                {
                    return new List<ChessPiece> { center, left, right, down };
                }

                // ⊣ (T sola)
                if (up && down && left &&
                    up.pieceColor == color &&
                    down.pieceColor == color &&
                    left.pieceColor == color)
                {
                    return new List<ChessPiece> { center, up, down, left };
                }

                // ⊢ (T sağa)
                if (up && down && right &&
                    up.pieceColor == color &&
                    down.pieceColor == color &&
                    right.pieceColor == color)
                {
                    return new List<ChessPiece> { center, up, down, right };
                }
            }
        }

        return new List<ChessPiece>();
    }
    void SpawnQueen(int x, int y, PieceColor color)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        GameObject queen = Instantiate(queenPrefab, gridOrigin);
        if (queenSound != null)
            AudioSource.PlayClipAtPoint(queenSound, Camera.main.transform.position);

        queen.transform.localPosition = new Vector3(
            x * spacedCellSize,
            y * spacedCellSize,
            0
        );

        ChessPiece piece = queen.GetComponent<ChessPiece>();
        piece.x = x;
        piece.y = y;
        piece.pieceColor = color;
        piece.pieceType = PieceType.Queen;

        grid[x, y] = piece;

        Debug.Log($"[QUEEN] Oluşturuldu ({x},{y}) Renk: {color}");
    }


    List<ChessPiece> FindKingMatches()
    {
        Vector2Int[][] patterns = new Vector2Int[][]
        {
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(1,0) },
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1) },
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(-1,0) },
        new[] { new Vector2Int(0,0), new Vector2Int(-1,0), new Vector2Int(-2,0), new Vector2Int(0,1) },
        };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPiece basePiece = grid[x, y];
                if (basePiece == null || basePiece.pieceType != PieceType.Normal)
                    continue;

                foreach (var pattern in patterns)
                {
                    List<ChessPiece> match = new List<ChessPiece>();
                    PieceColor color = basePiece.pieceColor;

                    bool valid = true;

                    foreach (var offset in pattern)
                    {
                        int nx = x + offset.x;
                        int ny = y + offset.y;

                        if (!IsValidPosition(nx, ny) ||
                            grid[nx, ny] == null ||
                            grid[nx, ny].pieceColor != color ||
                            grid[nx, ny].pieceType != PieceType.Normal)
                        {
                            valid = false;
                            break;
                        }

                        match.Add(grid[nx, ny]);
                    }

                    if (valid)
                        return match;
                }
            }
        }

        return new List<ChessPiece>();
    }
    void SpawnKing(int x, int y, PieceColor color)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        GameObject king = Instantiate(kingPrefab, gridOrigin);
        if (kingSound != null)
            AudioSource.PlayClipAtPoint(kingSound, Camera.main.transform.position);

        king.transform.localPosition = new Vector3(
            x * spacedCellSize,
            y * spacedCellSize,
            0
        );

        ChessPiece piece = king.GetComponent<ChessPiece>();
        piece.x = x;
        piece.y = y;
        piece.pieceColor = color;
        piece.pieceType = PieceType.King;

        grid[x, y] = piece;

        Debug.Log($"[KING] Oluşturuldu ({x},{y}) Renk: {color}");
    }
    List<ChessPiece> GetAllNormalPawns()
    {
        List<ChessPiece> pawns = new List<ChessPiece>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPiece p = grid[x, y];
                if (p != null && p.pieceType == PieceType.Normal)
                {
                    pawns.Add(p);
                }
            }
        }

        return pawns;
    }
    bool IsKnightPawnMatch(List<ChessPiece> match)
    {
        bool hasKnight = false;
        bool hasPawn = false;

        foreach (var p in match)
        {
            if (p.pieceType == PieceType.Knight) hasKnight = true;
            if (p.pieceType == PieceType.Normal) hasPawn = true;
        }

        return hasKnight && hasPawn;
    }
    IEnumerator SpawnKnightAttackers()
    {
        List<ChessPiece> pawns = new List<ChessPiece>();

        // SADECE NORMAL PİYONLAR
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null &&
                    grid[x, y].pieceType == PieceType.Normal)
                {
                    pawns.Add(grid[x, y]);
                }
            }
        }

        int attackCount = Mathf.Min(4, pawns.Count);
        float spacedCellSize = cellSize * spacingMultiplier;

        for (int i = 0; i < attackCount; i++)
        {
            ChessPiece target = pawns[Random.Range(0, pawns.Count)];
            pawns.Remove(target);

            if (target == null) continue;

            // 🐎 KNIGHT OLUŞTUR (GEÇİCİ)
            GameObject knight = Instantiate(knightPrefab, gridOrigin);
            knight.transform.position =
                target.transform.position + Vector3.up * knightSpawnHeight;

            // Hedef pozisyon
            Vector3 targetPos = new Vector3(
                target.x * spacedCellSize,
                target.y * spacedCellSize,
                0
            );

            // ✈️ UÇUŞ
            yield return StartCoroutine(
                FlyKnightToTarget(knight.transform, targetPos)
            );

            // 🔊 SES
            if (knightAttackSound != null)
                AudioSource.PlayClipAtPoint(knightAttackSound, targetPos);

            // 💥 EFEKT
            if (knightAttackEffect != null)
            {
                ParticleSystem fx = Instantiate(
                    knightAttackEffect,
                    targetPos,
                    Quaternion.identity
                );
                Destroy(fx.gameObject, 2f);
            }

            // ❌ PİYONU YOK ET
            grid[target.x, target.y] = null;
            Destroy(target.gameObject);

            // 🗑️ KNIGHT'I SİL
            Destroy(knight);

            yield return new WaitForSeconds(0.1f);
        }

        // ⬇️ ANINDA DÜŞÜR
        yield return StartCoroutine(DropPieces());
        yield return StartCoroutine(FillEmptySpaces());
    }

    IEnumerator FlyKnightToTarget(Transform knight, Vector3 targetPos)
    {
        Vector3 startPos = knight.position;
        float elapsed = 0f;

        while (elapsed < knightFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / knightFlyDuration;

            // Parabolik uçuş
            float height = Mathf.Sin(t * Mathf.PI) * 1.2f;

            knight.position = Vector3.Lerp(startPos, targetPos, t)
                               + Vector3.up * height;

            yield return null;
        }

        knight.position = targetPos;
    }


    void SpawnAttackerKnight(ChessPiece target)
    {
        Vector3 spawnPos = target.transform.position + Vector3.up * 3f;

        GameObject knight = Instantiate(
            knightPrefab,
            spawnPos,
            Quaternion.identity
        );

        StartCoroutine(KnightAttackRoutine(knight, target));
    }
    IEnumerator KnightAttackRoutine(GameObject knight, ChessPiece target)
    {
        float t = 0f;
        Vector3 start = knight.transform.position;
        Vector3 end = target.transform.position;

        while (t < 1f)
        {
            t += Time.deltaTime * 2.5f;
            knight.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        // Pawn yok et
        if (target != null)
        {
            grid[target.x, target.y] = null;

            if (matchEffectPrefab != null)
                Instantiate(matchEffectPrefab, target.transform.position, Quaternion.identity);

            Destroy(target.gameObject);
        }

        Destroy(knight);
    }



    IEnumerator ActivateRookPower(ChessPiece rook)
    {
        if (rook == null)
            yield break;

        int rx = rook.x;
        int ry = rook.y;

        List<ChessPiece> toDestroy = new List<ChessPiece>();

        // 🟦 SATIR (YATAY)
        for (int x = 0; x < width; x++)
        {
            if (grid[x, ry] != null && grid[x, ry] != rook)
            {
                toDestroy.Add(grid[x, ry]);
            }
        }

        // 🟦 SÜTUN (DİKEY)
        for (int y = 0; y < height; y++)
        {
            if (grid[rx, y] != null && grid[rx, y] != rook)
            {
                toDestroy.Add(grid[rx, y]);
            }
        }

        // 🔊 SES
        if (rookSound != null)
            AudioSource.PlayClipAtPoint(rookSound, Camera.main.transform.position);

        yield return new WaitForSeconds(0.05f);

        // 💥 YOK ETME
        foreach (ChessPiece piece in toDestroy)
        {
            if (piece == null) continue;

            if (matchEffectPrefab != null)
            {
                ParticleSystem fx = Instantiate(
                    matchEffectPrefab,
                    piece.transform.position,
                    Quaternion.identity
                );
                Destroy(fx.gameObject, 2f);
            }

            grid[piece.x, piece.y] = null;
            Destroy(piece.gameObject);
        }

        // ❌ KALEYİ DE YOK ET
        grid[rook.x, rook.y] = null;
        Destroy(rook.gameObject);

        // ⬇️ ANINDA DÜŞÜR
        yield return StartCoroutine(DropPieces());
        yield return StartCoroutine(FillEmptySpaces());
    }

}