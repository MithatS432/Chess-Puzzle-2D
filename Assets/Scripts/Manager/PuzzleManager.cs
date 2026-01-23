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

    public GameObject destroyEffect;
    public GameObject kingCenterExplosionPrefab;

    public AudioClip knightHitSound;


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


    private MissionManager missionManager;



    void Start()
    {
        UpdateUI();
        InitUI();

        grid = new ChessPiece[width, height];

        CalculateCellSize();
        CenterGrid();
        CreateGrid();

        missionManager = FindAnyObjectByType<MissionManager>();

        if (missionManager != null)
        {
            levelIndex = missionManager.GetCurrentLevel();
            levelText.text = levelIndex.ToString();
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }
        if (missionManager != null && winPanel != null)
        {
            missionManager.SetWinPanel(winPanel);
        }
    }
    int GetCurrentLevelFromMissionManager()
    {
        if (missionManager != null)
        {
            // MissionManager'da GetCurrentLevel() metodunu public yaptık
            return missionManager.GetCurrentLevel();
        }
        return 1; // Varsayılan
    }
    IEnumerator CheckAndResolveMatchesAfterSwap()
    {
        int comboStep = 0;

        while (true)
        {
            List<ChessPiece> matches = FindAllMatches();

            if (matches.Count == 0)
            {
                if (comboStep > 0)
                {
                    if (missionManager != null) missionManager.OnComboPerformed(comboStep);
                }
                break;
            }

            comboStep++;
            Debug.Log($"[Combo] Adım {comboStep}: {matches.Count} eşleşme!");

            yield return StartCoroutine(DestroyMatches(matches));
            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());

            yield return new WaitForSeconds(0.3f);
        }

        isProcessingMatches = false;
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
        // Rook kontrolü
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

        // ✅ KING KONTROLÜ (TAM 5'Lİ DÜZ ÇİZGİ)
        if (kingMatch.Count == 5)
        {
            ChessPiece center = kingMatch[2]; // Ortadaki tile
            int kx = center.x;
            int ky = center.y;
            PieceColor color = center.pieceColor;

            Debug.Log($"[KING] 5'li düz çizgi onaylandı! King oluşturuluyor.");

            foreach (ChessPiece p in kingMatch)
            {
                if (matchEffectPrefab != null)
                {
                    ParticleSystem effect = Instantiate(matchEffectPrefab,
                        p.transform.position,
                        Quaternion.identity);
                    Destroy(effect.gameObject, 2f);
                }
                grid[p.x, p.y] = null;
                Destroy(p.gameObject);
            }

            yield return new WaitForSeconds(0.15f);
            SpawnKing(kx, ky, color);
            yield break;
        }

        // Queen kontrolü
        if (queenMatch.Count == 5)
        {
            ChessPiece center = queenMatch[0];
            int qx = center.x;
            int qy = center.y;
            PieceColor color = center.pieceColor;

            Debug.Log($"[QUEEN] 5 tile L şekli onaylandı! Queen oluşturuluyor.");

            foreach (ChessPiece p in queenMatch)
            {
                if (matchEffectPrefab != null)
                {
                    ParticleSystem effect = Instantiate(matchEffectPrefab,
                        p.transform.position,
                        Quaternion.identity);
                    Destroy(effect.gameObject, 2f);
                }
                grid[p.x, p.y] = null;
                Destroy(p.gameObject);
            }

            yield return new WaitForSeconds(0.15f);
            SpawnQueen(qx, qy, color);
            yield break;
        }

        // Bishop kontrolü (bu da 5'li olmalı değil mi?)
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

        // Rook kontrolü
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

        // 2x2 Square (Knight)
        if (squareMatches.Count == 4)
        {
            Debug.Log("[2x2 Square] 4 tile yok edilip Knight oluşturuluyor!");

            ChessPiece bottomLeft = squareMatches[0];
            int knightX = bottomLeft.x;
            int knightY = bottomLeft.y;
            PieceColor knightColor = bottomLeft.pieceColor;

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

            foreach (ChessPiece piece in squareMatches)
            {
                matches.Remove(piece);
            }
        }

        // Normal match yok etme
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
        if (missionManager != null) missionManager.OnPiecesCleared(matches.Count);

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

        lastSwappedA = piece1;
        lastSwappedB = piece2;

        moveCountLeft--;
        UpdateUI();

        // ================= SWAP =================
        int tempX = piece1.x;
        int tempY = piece1.y;

        grid[piece1.x, piece1.y] = piece2;
        grid[piece2.x, piece2.y] = piece1;

        piece1.x = piece2.x;
        piece1.y = piece2.y;
        piece2.x = tempX;
        piece2.y = tempY;

        float spacedCellSize = cellSize * spacingMultiplier;

        Vector3 piece1Target = new Vector3(
            piece1.x * spacedCellSize,
            piece1.y * spacedCellSize,
            0
        );

        Vector3 piece2Target = new Vector3(
            piece2.x * spacedCellSize,
            piece2.y * spacedCellSize,
            0
        );

        StartCoroutine(MovePiece(piece1.transform, piece1Target, 0.2f));
        StartCoroutine(MovePiece(piece2.transform, piece2Target, 0.2f));

        yield return new WaitForSeconds(0.25f);

        // ================= KING + NORMAL PIYON KONTROL =================
        bool isKingPawnSwap =
            (piece1.pieceType == PieceType.King && piece2.pieceType == PieceType.Normal) ||
            (piece1.pieceType == PieceType.Normal && piece2.pieceType == PieceType.King);

        if (isKingPawnSwap)
        {
            ChessPiece king = (piece1.pieceType == PieceType.King) ? piece1 : piece2;

            Debug.Log($"[KING POWER] Şah + Piyon eşleşmesi! 4x4 alan patlaması: ({king.x}, {king.y})");

            yield return StartCoroutine(ActivateKingPower(king));

            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());

            isProcessingMatches = false;
            yield break;
        }


        // ================= ROOK + NORMAL PIYON KONTROL =================
        bool isRookPawnSwap =
            (piece1.pieceType == PieceType.Rook && piece2.pieceType == PieceType.Normal) ||
            (piece1.pieceType == PieceType.Normal && piece2.pieceType == PieceType.Rook);

        if (isRookPawnSwap)
        {
            ChessPiece rook = (piece1.pieceType == PieceType.Rook) ? piece1 : piece2;

            Debug.Log($"[ROOK POWER] Kale + Piyon eşleşmesi! Satır ve sütun yok ediliyor: ({rook.x}, {rook.y})");

            yield return StartCoroutine(ActivateRookPower(rook));

            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());

            isProcessingMatches = false;
            yield break;
        }

        // ================= BISHOP + NORMAL PIYON KONTROL =================
        bool isBishopPawnSwap =
            (piece1.pieceType == PieceType.Bishop && piece2.pieceType == PieceType.Normal) ||
            (piece1.pieceType == PieceType.Normal && piece2.pieceType == PieceType.Bishop);

        if (isBishopPawnSwap)
        {
            ChessPiece bishop = (piece1.pieceType == PieceType.Bishop) ? piece1 : piece2;

            Debug.Log($"[BISHOP POWER] Fil + Piyon eşleşmesi! Tüm çaprazlar yok ediliyor: ({bishop.x}, {bishop.y})");

            yield return StartCoroutine(ActivateBishopPower(bishop));

            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());

            isProcessingMatches = false;
            yield break;
        }

        // ================= QUEEN + NORMAL PIYON KONTROL =================
        bool isQueenPawnSwap =
            (piece1.pieceType == PieceType.Queen && piece2.pieceType == PieceType.Normal) ||
            (piece1.pieceType == PieceType.Normal && piece2.pieceType == PieceType.Queen);

        if (isQueenPawnSwap)
        {
            ChessPiece queen = (piece1.pieceType == PieceType.Queen) ? piece1 : piece2;

            Debug.Log($"[QUEEN POWER] Vezir + Piyon eşleşmesi! 8 yöndeki tüm taşlar yok ediliyor: ({queen.x}, {queen.y})");

            yield return StartCoroutine(ActivateQueenPower(queen));

            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());

            isProcessingMatches = false;
            yield break;
        }

        // ================= KNIGHT + NORMAL PIYON KONTROL =================
        bool isKnightPawnSwap =
            (piece1.pieceType == PieceType.Knight && piece2.pieceType == PieceType.Normal) ||
            (piece1.pieceType == PieceType.Normal && piece2.pieceType == PieceType.Knight);

        if (isKnightPawnSwap)
        {
            Debug.Log("[KNIGHT COMBO] Knight + Piyon eşleşmesi! 4 at saldırısı başlıyor!");

            if (matchEffectPrefab != null)
            {
                Instantiate(matchEffectPrefab, piece1.transform.position, Quaternion.identity);
                Instantiate(matchEffectPrefab, piece2.transform.position, Quaternion.identity);
            }

            grid[piece1.x, piece1.y] = null;
            grid[piece2.x, piece2.y] = null;
            Destroy(piece1.gameObject);
            Destroy(piece2.gameObject);

            yield return new WaitForSeconds(0.2f);

            yield return StartCoroutine(KnightComboAttack());

            yield return StartCoroutine(DropPieces());
            yield return StartCoroutine(FillEmptySpaces());
            yield return StartCoroutine(CheckAndResolveMatchesAfterSwap());

            isProcessingMatches = false;
            yield break;
        }

        // ================= NORMAL MATCH =================
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
            // ❌ Match yok → geri al
            int temp2X = piece1.x;
            int temp2Y = piece1.y;

            grid[piece1.x, piece1.y] = piece2;
            grid[piece2.x, piece2.y] = piece1;

            piece1.x = piece2.x;
            piece1.y = piece2.y;
            piece2.x = temp2X;
            piece2.y = temp2Y;

            Vector3 piece1Original = new Vector3(
                piece1.x * spacedCellSize,
                piece1.y * spacedCellSize,
                0
            );

            Vector3 piece2Original = new Vector3(
                piece2.x * spacedCellSize,
                piece2.y * spacedCellSize,
                0
            );

            StartCoroutine(MovePiece(piece1.transform, piece1Original, 0.15f));
            StartCoroutine(MovePiece(piece2.transform, piece2Original, 0.15f));

            yield return new WaitForSeconds(0.2f);
        }

        isProcessingMatches = false;

        // ================= LOSE KONTROL =================
        if (moveCountLeft <= 0 && !hasGameEnded)
        {
            hasGameEnded = true;


            if (loseSound != null)
                AudioSource.PlayClipAtPoint(loseSound, Camera.main.transform.position);

            losePanel.SetActive(true);
            Invoke("RestartLevel", 3f);
        }

        yield return null;
    }
    void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    IEnumerator KnightComboAttack()
    {
        // Tüm normal piyonları topla (özel taşlar hariç)
        List<ChessPiece> allNormalPawns = new List<ChessPiece>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPiece p = grid[x, y];
                if (p != null && p.pieceType == PieceType.Normal)
                {
                    allNormalPawns.Add(p);
                }
            }
        }

        if (allNormalPawns.Count == 0)
        {
            Debug.Log("[KNIGHT COMBO] Saldırı için hedef bulunamadı!");
            yield break;
        }

        // 4 rastgele hedef seç (veya mevcut sayı kadarını seç)
        int attackCount = Mathf.Min(4, allNormalPawns.Count);
        List<ChessPiece> targets = new List<ChessPiece>();

        for (int i = 0; i < attackCount; i++)
        {
            int randomIndex = Random.Range(0, allNormalPawns.Count);
            targets.Add(allNormalPawns[randomIndex]);
            allNormalPawns.RemoveAt(randomIndex);
        }

        Debug.Log($"[KNIGHT COMBO] {targets.Count} hedefe saldırı başlatılıyor!");

        // 4 at oluştur ve hedeflere saldır
        yield return StartCoroutine(SpawnAndAttackWithKnights(targets));
    }

    IEnumerator SpawnAndAttackWithKnights(List<ChessPiece> targets)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        foreach (ChessPiece target in targets)
        {
            if (target == null || grid[target.x, target.y] == null)
                continue;

            // 🎯 Hedefin pozisyonu
            Vector3 targetLocalPos = new Vector3(
                target.x * spacedCellSize,
                target.y * spacedCellSize,
                0
            );
            Vector3 targetWorldPos = gridOrigin.TransformPoint(targetLocalPos);

            // 🐴 At'ın spawn pozisyonu (rastgele kenardan)
            Vector3 spawnPos = GetRandomEdgePosition(targetWorldPos);

            // ♞ Knight oluştur
            GameObject knight = Instantiate(knightPrefab, spawnPos, Quaternion.identity);
            knight.transform.localScale = knightPrefab.transform.localScale;

            // 🔊 Ses efekti
            if (knightSound != null)
                AudioSource.PlayClipAtPoint(knightSound, Camera.main.transform.position);

            // ✈️ Hedefe doğru hareket
            yield return StartCoroutine(FlyKnightToTarget(knight.transform, targetWorldPos));

            // 💥 Hedefi yok et
            if (destroyEffect != null)
                Instantiate(destroyEffect, targetWorldPos, Quaternion.identity);

            if (knightHitSound != null)
                AudioSource.PlayClipAtPoint(knightHitSound, Camera.main.transform.position);

            grid[target.x, target.y] = null;
            Destroy(target.gameObject);
            Destroy(knight);

            yield return new WaitForSeconds(0.1f);
        }
    }
    Vector3 GetRandomEdgePosition(Vector3 targetPos)
    {
        // Rastgele bir kenar seç (0=sol, 1=sağ, 2=üst, 3=alt)
        int edge = Random.Range(0, 4);
        float offset = 5f; // Ekran dışından ne kadar uzakta spawn olacak

        switch (edge)
        {
            case 0: // Sol
                return new Vector3(targetPos.x - offset, targetPos.y, targetPos.z);
            case 1: // Sağ
                return new Vector3(targetPos.x + offset, targetPos.y, targetPos.z);
            case 2: // Üst
                return new Vector3(targetPos.x, targetPos.y + offset, targetPos.z);
            case 3: // Alt
            default:
                return new Vector3(targetPos.x, targetPos.y - offset, targetPos.z);
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

        if (missionManager != null) missionManager.OnKnightCreated();

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

        if (missionManager != null) missionManager.OnRookCreated();

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
        // T ŞEKLİ (5 tile) patternleri - BISHOP için
        // T şekilleri:
        // 1. T üst:   X X X
        //                X
        //                X
        // 2. T sağ:     X
        //             X X X
        //                X
        // 3. T alt:      X
        //                X
        //             X X X
        // 4. T sol:      X
        //             X X X
        //                X

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPiece center = grid[x, y];
                if (center == null || center.pieceType != PieceType.Normal)
                    continue;

                PieceColor color = center.pieceColor;

                // ==========================================
                // T ÜST (T shape - upward) - 5 tile
                //   X X X
                //     X
                //     X
                // ==========================================
                if (IsValidPosition(x - 1, y) && IsValidPosition(x + 1, y) &&
                    IsValidPosition(x, y - 1) && IsValidPosition(x, y - 2))
                {
                    if (grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x, y - 2] != null && grid[x, y - 2].pieceType == PieceType.Normal && grid[x, y - 2].pieceColor == color)
                    {
                        Debug.Log($"[BISHOP] T Üst şekli bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x - 1, y], grid[x + 1, y], grid[x, y - 1], grid[x, y - 2] };
                    }
                }

                // ==========================================
                // T SAĞ (T shape - right) - 5 tile
                //     X
                //   X X X
                //     X
                // ==========================================
                if (IsValidPosition(x, y + 1) && IsValidPosition(x, y - 1) &&
                    IsValidPosition(x + 1, y) && IsValidPosition(x + 2, y))
                {
                    if (grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x + 2, y] != null && grid[x + 2, y].pieceType == PieceType.Normal && grid[x + 2, y].pieceColor == color)
                    {
                        Debug.Log($"[BISHOP] T Sağ şekli bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x, y + 1], grid[x, y - 1], grid[x + 1, y], grid[x + 2, y] };
                    }
                }

                // ==========================================
                // T ALT (T shape - downward) - 5 tile
                //     X
                //     X
                //   X X X
                // ==========================================
                if (IsValidPosition(x - 1, y) && IsValidPosition(x + 1, y) &&
                    IsValidPosition(x, y + 1) && IsValidPosition(x, y + 2))
                {
                    if (grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y + 2] != null && grid[x, y + 2].pieceType == PieceType.Normal && grid[x, y + 2].pieceColor == color)
                    {
                        Debug.Log($"[BISHOP] T Alt şekli bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x - 1, y], grid[x + 1, y], grid[x, y + 1], grid[x, y + 2] };
                    }
                }

                // ==========================================
                // T SOL (T shape - left) - 5 tile
                //     X
                //   X X X
                //     X
                // (Not: Bu aslında T Sağ'ın yatay simetrisi)
                // ==========================================
                if (IsValidPosition(x, y + 1) && IsValidPosition(x, y - 1) &&
                    IsValidPosition(x - 1, y) && IsValidPosition(x - 2, y))
                {
                    if (grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x - 2, y] != null && grid[x - 2, y].pieceType == PieceType.Normal && grid[x - 2, y].pieceColor == color)
                    {
                        Debug.Log($"[BISHOP] T Sol şekli bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x, y + 1], grid[x, y - 1], grid[x - 1, y], grid[x - 2, y] };
                    }
                }

                // ==========================================
                // + ŞEKLİ (Çapraz T) - 5 tile
                //     X
                //   X X X
                //     X
                // ==========================================
                if (IsValidPosition(x - 1, y) && IsValidPosition(x + 1, y) &&
                    IsValidPosition(x, y - 1) && IsValidPosition(x, y + 1))
                {
                    if (grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color)
                    {
                        Debug.Log($"[BISHOP] + Şekli bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x - 1, y], grid[x + 1, y], grid[x, y - 1], grid[x, y + 1] };
                    }
                }
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

        if (missionManager != null) missionManager.OnBishopCreated();

    }



    List<ChessPiece> FindQueenMatches()
    {
        // L şekli patternleri (5 tile)
        // Her pattern: merkez + 4 yön

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPiece center = grid[x, y];
                if (center == null || center.pieceType != PieceType.Normal)
                    continue;

                PieceColor color = center.pieceColor;

                // ═══════════════════════════════════
                // L ŞEKLİ 1: ⅃ (Sol üst köşe)
                //     X
                //     X
                // X X X
                // ═══════════════════════════════════
                if (IsValidPosition(x, y + 1) && IsValidPosition(x, y + 2) &&
                    IsValidPosition(x - 1, y) && IsValidPosition(x - 2, y))
                {
                    if (grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y + 2] != null && grid[x, y + 2].pieceType == PieceType.Normal && grid[x, y + 2].pieceColor == color &&
                        grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x - 2, y] != null && grid[x - 2, y].pieceType == PieceType.Normal && grid[x - 2, y].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 1 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x, y + 1], grid[x, y + 2], grid[x - 1, y], grid[x - 2, y] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 2: L (Sağ üst köşe)
                // X
                // X
                // X X X
                // ═══════════════════════════════════
                if (IsValidPosition(x, y + 1) && IsValidPosition(x, y + 2) &&
                    IsValidPosition(x + 1, y) && IsValidPosition(x + 2, y))
                {
                    if (grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y + 2] != null && grid[x, y + 2].pieceType == PieceType.Normal && grid[x, y + 2].pieceColor == color &&
                        grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x + 2, y] != null && grid[x + 2, y].pieceType == PieceType.Normal && grid[x + 2, y].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 2 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x, y + 1], grid[x, y + 2], grid[x + 1, y], grid[x + 2, y] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 3: Ꞁ (Sol alt köşe)
                // X X X
                //     X
                //     X
                // ═══════════════════════════════════
                if (IsValidPosition(x, y - 1) && IsValidPosition(x, y - 2) &&
                    IsValidPosition(x - 1, y) && IsValidPosition(x - 2, y))
                {
                    if (grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x, y - 2] != null && grid[x, y - 2].pieceType == PieceType.Normal && grid[x, y - 2].pieceColor == color &&
                        grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x - 2, y] != null && grid[x - 2, y].pieceType == PieceType.Normal && grid[x - 2, y].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 3 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x, y - 1], grid[x, y - 2], grid[x - 1, y], grid[x - 2, y] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 4: ⅂ (Sağ alt köşe)
                // X X X
                // X
                // X
                // ═══════════════════════════════════
                if (IsValidPosition(x, y - 1) && IsValidPosition(x, y - 2) &&
                    IsValidPosition(x + 1, y) && IsValidPosition(x + 2, y))
                {
                    if (grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x, y - 2] != null && grid[x, y - 2].pieceType == PieceType.Normal && grid[x, y - 2].pieceColor == color &&
                        grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x + 2, y] != null && grid[x + 2, y].pieceType == PieceType.Normal && grid[x + 2, y].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 4 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x, y - 1], grid[x, y - 2], grid[x + 1, y], grid[x + 2, y] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 5: ⌐ (Yukarı bakan L - sola)
                // X X
                //   X
                //   X
                //   X
                // ═══════════════════════════════════
                if (IsValidPosition(x - 1, y) && IsValidPosition(x, y - 1) &&
                    IsValidPosition(x, y - 2) && IsValidPosition(x, y - 3))
                {
                    if (grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x, y - 2] != null && grid[x, y - 2].pieceType == PieceType.Normal && grid[x, y - 2].pieceColor == color &&
                        grid[x, y - 3] != null && grid[x, y - 3].pieceType == PieceType.Normal && grid[x, y - 3].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 5 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x - 1, y], grid[x, y - 1], grid[x, y - 2], grid[x, y - 3] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 6: ¬ (Yukarı bakan L - sağa)
                //   X X
                //   X
                //   X
                //   X
                // ═══════════════════════════════════
                if (IsValidPosition(x + 1, y) && IsValidPosition(x, y - 1) &&
                    IsValidPosition(x, y - 2) && IsValidPosition(x, y - 3))
                {
                    if (grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x, y - 1] != null && grid[x, y - 1].pieceType == PieceType.Normal && grid[x, y - 1].pieceColor == color &&
                        grid[x, y - 2] != null && grid[x, y - 2].pieceType == PieceType.Normal && grid[x, y - 2].pieceColor == color &&
                        grid[x, y - 3] != null && grid[x, y - 3].pieceType == PieceType.Normal && grid[x, y - 3].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 6 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x + 1, y], grid[x, y - 1], grid[x, y - 2], grid[x, y - 3] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 7: ⌙ (Aşağı bakan L - sola)
                //   X
                //   X
                //   X
                // X X
                // ═══════════════════════════════════
                if (IsValidPosition(x - 1, y) && IsValidPosition(x, y + 1) &&
                    IsValidPosition(x, y + 2) && IsValidPosition(x, y + 3))
                {
                    if (grid[x - 1, y] != null && grid[x - 1, y].pieceType == PieceType.Normal && grid[x - 1, y].pieceColor == color &&
                        grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y + 2] != null && grid[x, y + 2].pieceType == PieceType.Normal && grid[x, y + 2].pieceColor == color &&
                        grid[x, y + 3] != null && grid[x, y + 3].pieceType == PieceType.Normal && grid[x, y + 3].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 7 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x - 1, y], grid[x, y + 1], grid[x, y + 2], grid[x, y + 3] };
                    }
                }

                // ═══════════════════════════════════
                // L ŞEKLİ 8: ⌐ (Aşağı bakan L - sağa)
                // X
                // X
                // X
                // X X
                // ═══════════════════════════════════
                if (IsValidPosition(x + 1, y) && IsValidPosition(x, y + 1) &&
                    IsValidPosition(x, y + 2) && IsValidPosition(x, y + 3))
                {
                    if (grid[x + 1, y] != null && grid[x + 1, y].pieceType == PieceType.Normal && grid[x + 1, y].pieceColor == color &&
                        grid[x, y + 1] != null && grid[x, y + 1].pieceType == PieceType.Normal && grid[x, y + 1].pieceColor == color &&
                        grid[x, y + 2] != null && grid[x, y + 2].pieceType == PieceType.Normal && grid[x, y + 2].pieceColor == color &&
                        grid[x, y + 3] != null && grid[x, y + 3].pieceType == PieceType.Normal && grid[x, y + 3].pieceColor == color)
                    {
                        Debug.Log($"[QUEEN] L Şekli 8 bulundu: ({x},{y})");
                        return new List<ChessPiece> { center, grid[x + 1, y], grid[x, y + 1], grid[x, y + 2], grid[x, y + 3] };
                    }
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

        if (missionManager != null) missionManager.OnQueenCreated();

    }


    List<ChessPiece> FindKingMatches()
    {
        // Sadece 5'li düz çizgi (yatay veya dikey)

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPiece first = grid[x, y];
                if (first == null || first.pieceType != PieceType.Normal)
                    continue;

                PieceColor color = first.pieceColor;

                // ═══════════════════════════════════
                // YATAY 5'Lİ (→→→→→)
                // X X X X X
                // ═══════════════════════════════════
                if (x <= width - 5)
                {
                    bool isMatch = true;
                    List<ChessPiece> horizontalMatch = new List<ChessPiece>();

                    for (int i = 0; i < 5; i++)
                    {
                        ChessPiece piece = grid[x + i, y];

                        if (piece == null ||
                            piece.pieceType != PieceType.Normal ||
                            piece.pieceColor != color)
                        {
                            isMatch = false;
                            break;
                        }

                        horizontalMatch.Add(piece);
                    }

                    if (isMatch)
                    {
                        Debug.Log($"[KING] 5'li YATAY çizgi bulundu: ({x},{y})");
                        return horizontalMatch;
                    }
                }

                // ═══════════════════════════════════
                // DİKEY 5'Lİ (↑↑↑↑↑)
                // X
                // X
                // X
                // X
                // X
                // ═══════════════════════════════════
                if (y <= height - 5)
                {
                    bool isMatch = true;
                    List<ChessPiece> verticalMatch = new List<ChessPiece>();

                    for (int i = 0; i < 5; i++)
                    {
                        ChessPiece piece = grid[x, y + i];

                        if (piece == null ||
                            piece.pieceType != PieceType.Normal ||
                            piece.pieceColor != color)
                        {
                            isMatch = false;
                            break;
                        }

                        verticalMatch.Add(piece);
                    }

                    if (isMatch)
                    {
                        Debug.Log($"[KING] 5'li DİKEY çizgi bulundu: ({x},{y})");
                        return verticalMatch;
                    }
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

        if (missionManager != null) missionManager.OnKingCreated();

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
        if (match == null || match.Count == 0)
            return false;

        bool hasKnight = false;
        bool hasPawn = false;

        foreach (var p in match)
        {
            if (p == null) continue;

            if (p.pieceType == PieceType.Knight)
                hasKnight = true;
            if (p.pieceType == PieceType.Normal)
                hasPawn = true;
        }

        bool result = hasKnight && hasPawn;

        if (result)
        {
            Debug.Log($"[KNIGHT MATCH] Knight: {hasKnight}, Pawn: {hasPawn}, Total: {match.Count}");
        }

        return result;
    }
    IEnumerator SpawnKnightAttackers(List<ChessPiece> targetPawns)
    {
        float spacedCellSize = cellSize * spacingMultiplier;

        foreach (ChessPiece target in targetPawns)
        {
            if (target == null)
                continue;

            // 🎯 Hedefin WORLD pozisyonunu al
            Vector3 targetWorldPos = gridOrigin.TransformPoint(
                new Vector3(
                    target.x * spacedCellSize,
                    target.y * spacedCellSize,
                    0
                )
            );

            // 🚁 Spawn pozisyonu (hedefin üstünde)
            Vector3 spawnPos = targetWorldPos + Vector3.up * knightSpawnHeight;

            // ♞ Knight oluştur (WORLD space'de, parent YOK)
            GameObject knight = Instantiate(
                knightPrefab,
                spawnPos,
                Quaternion.identity
            );

            // ✈️ Hedefe uç
            yield return StartCoroutine(
                FlyKnightToTarget(knight.transform, targetWorldPos)
            );

            // 💥 GRID TEMİZLE
            grid[target.x, target.y] = null;

            // 💥 EFEKT
            if (destroyEffect != null)
                Instantiate(destroyEffect, targetWorldPos, Quaternion.identity);

            // 🔊 SES
            if (knightHitSound != null)
                AudioSource.PlayClipAtPoint(
                    knightHitSound,
                    Camera.main.transform.position
                );

            // 🗑️ Yok et
            Destroy(target.gameObject);
            Destroy(knight);

            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator FlyKnightToTarget(Transform knight, Vector3 targetPos)
    {
        float elapsed = 0f;
        float duration = 0.4f;
        Vector3 startPos = knight.position;

        while (elapsed < duration)
        {
            if (knight == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Smooth hareket
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            knight.position = Vector3.Lerp(startPos, targetPos, smoothT);

            yield return null;
        }

        if (knight != null)
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

        Debug.Log($"[ROOK POWER] Kale pozisyonu: ({rx}, {ry})");

        // 🟦 SATIR (YATAY) - Tüm x değerleri, sabit y
        for (int x = 0; x < width; x++)
        {
            if (grid[x, ry] != null)
            {
                toDestroy.Add(grid[x, ry]);
                Debug.Log($"[ROOK] Satırdan eklendi: ({x}, {ry})");
            }
        }

        // 🟦 SÜTUN (DİKEY) - Sabit x, tüm y değerleri
        for (int y = 0; y < height; y++)
        {
            if (grid[rx, y] != null && !toDestroy.Contains(grid[rx, y]))
            {
                toDestroy.Add(grid[rx, y]);
                Debug.Log($"[ROOK] Sütundan eklendi: ({rx}, {y})");
            }
        }

        Debug.Log($"[ROOK POWER] Toplam {toDestroy.Count} taş yok edilecek!");

        // 🔊 SES
        if (rookSound != null)
            AudioSource.PlayClipAtPoint(rookSound, Camera.main.transform.position);

        // 💥 Önce yatay çizgiyi yok et (görsel efekt için)
        yield return StartCoroutine(DestroyLineWithEffect(toDestroy, rx, ry, true));

        yield return new WaitForSeconds(0.1f);

        // 💥 Sonra dikey çizgiyi yok et
        yield return StartCoroutine(DestroyLineWithEffect(toDestroy, rx, ry, false));

        if (missionManager != null) missionManager.OnPowerUpUsed();

    }
    IEnumerator DestroyLineWithEffect(List<ChessPiece> allPieces, int rookX, int rookY, bool isHorizontal)
    {
        List<ChessPiece> linePieces = new List<ChessPiece>();

        // Sadece bu çizgideki taşları al
        foreach (ChessPiece piece in allPieces)
        {
            if (piece == null) continue;

            if (isHorizontal && piece.y == rookY)
            {
                linePieces.Add(piece);
            }
            else if (!isHorizontal && piece.x == rookX)
            {
                linePieces.Add(piece);
            }
        }

        // Kaleden dışa doğru yok et
        if (isHorizontal)
        {
            // Önce soldan sağa
            linePieces.Sort((a, b) => Mathf.Abs(a.x - rookX).CompareTo(Mathf.Abs(b.x - rookX)));
        }
        else
        {
            // Aşağıdan yukarıya
            linePieces.Sort((a, b) => Mathf.Abs(a.y - rookY).CompareTo(Mathf.Abs(b.y - rookY)));
        }

        foreach (ChessPiece piece in linePieces)
        {
            if (piece == null) continue;

            // Efekt
            if (matchEffectPrefab != null)
            {
                ParticleSystem fx = Instantiate(
                    matchEffectPrefab,
                    piece.transform.position,
                    Quaternion.identity
                );
                Destroy(fx.gameObject, 2f);
            }

            // Grid'den çıkar
            if (grid[piece.x, piece.y] == piece)
            {
                grid[piece.x, piece.y] = null;
            }

            // Yok et
            Destroy(piece.gameObject);

            yield return new WaitForSeconds(0.05f); // Dalga efekti
        }
    }
    IEnumerator ActivateBishopPower(ChessPiece bishop)
    {
        if (bishop == null)
            yield break;

        int bx = bishop.x;
        int by = bishop.y;
        PieceColor color = bishop.pieceColor; // Renk önemli olabilir

        List<ChessPiece> toDestroy = new List<ChessPiece>();

        Debug.Log($"[BISHOP POWER] Fil pozisyonu: ({bx}, {by})");

        // 🔊 SES
        if (bishopSound != null)
            AudioSource.PlayClipAtPoint(bishopSound, Camera.main.transform.position);

        // ==========================================
        // 4 ÇAPRAZ YÖN
        // ==========================================

        // 1. SAĞ ÜST ÇAPRAZ (↗)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = bx + i;
            int ny = by + i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[BISHOP] Sağ üst çapraz: ({nx}, {ny})");
            }
        }

        // 2. SAĞ ALT ÇAPRAZ (↘)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = bx + i;
            int ny = by - i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[BISHOP] Sağ alt çapraz: ({nx}, {ny})");
            }
        }

        // 3. SOL ÜST ÇAPRAZ (↖)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = bx - i;
            int ny = by + i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[BISHOP] Sol üst çapraz: ({nx}, {ny})");
            }
        }

        // 4. SOL ALT ÇAPRAZ (↙)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = bx - i;
            int ny = by - i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[BISHOP] Sol alt çapraz: ({nx}, {ny})");
            }
        }

        Debug.Log($"[BISHOP POWER] Toplam {toDestroy.Count} taş yok edilecek!");

        // 💥 EFEFEKT VE YOK ETME
        foreach (ChessPiece piece in toDestroy)
        {
            if (piece == null) continue;

            // Efekt
            if (matchEffectPrefab != null)
            {
                ParticleSystem fx = Instantiate(
                    matchEffectPrefab,
                    piece.transform.position,
                    Quaternion.identity
                );
                Destroy(fx.gameObject, 2f);
            }

            // Grid'den çıkar
            if (grid[piece.x, piece.y] == piece)
            {
                grid[piece.x, piece.y] = null;
            }

            // Yok et
            Destroy(piece.gameObject);

            if (missionManager != null) missionManager.OnPowerUpUsed();

            yield return new WaitForSeconds(0.05f); // Dalga efekti
        }

        // Fili de yok et (opsiyonel - istersen silme)
        if (grid[bx, by] == bishop)
        {
            grid[bx, by] = null;
            Destroy(bishop.gameObject);
        }

        yield return new WaitForSeconds(0.2f);
    }
    IEnumerator ActivateQueenPower(ChessPiece queen)
    {
        if (queen == null)
            yield break;

        int qx = queen.x;
        int qy = queen.y;

        List<ChessPiece> toDestroy = new List<ChessPiece>();

        Debug.Log($"[QUEEN POWER] Vezir pozisyonu: ({qx}, {qy})");

        // 🔊 SES
        if (queenSound != null)
            AudioSource.PlayClipAtPoint(queenSound, Camera.main.transform.position);

        // ==========================================
        // 8 YÖN: Kale (4 yön) + Fil (4 yön)
        // ==========================================

        // 1. YATAY SAĞ (→)
        for (int i = 1; i < width; i++)
        {
            int nx = qx + i;
            int ny = qy;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Sağ: ({nx}, {ny})");
            }
        }

        // 2. YATAY SOL (←)
        for (int i = 1; i < width; i++)
        {
            int nx = qx - i;
            int ny = qy;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Sol: ({nx}, {ny})");
            }
        }

        // 3. DİKEY YUKARI (↑)
        for (int i = 1; i < height; i++)
        {
            int nx = qx;
            int ny = qy + i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Yukarı: ({nx}, {ny})");
            }
        }

        // 4. DİKEY AŞAĞI (↓)
        for (int i = 1; i < height; i++)
        {
            int nx = qx;
            int ny = qy - i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Aşağı: ({nx}, {ny})");
            }
        }

        // 5. SAĞ ÜST ÇAPRAZ (↗)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = qx + i;
            int ny = qy + i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Sağ üst çapraz: ({nx}, {ny})");
            }
        }

        // 6. SAĞ ALT ÇAPRAZ (↘)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = qx + i;
            int ny = qy - i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Sağ alt çapraz: ({nx}, {ny})");
            }
        }

        // 7. SOL ÜST ÇAPRAZ (↖)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = qx - i;
            int ny = qy + i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Sol üst çapraz: ({nx}, {ny})");
            }
        }

        // 8. SOL ALT ÇAPRAZ (↙)
        for (int i = 1; i < Mathf.Max(width, height); i++)
        {
            int nx = qx - i;
            int ny = qy - i;

            if (!IsValidPosition(nx, ny))
                break;

            if (grid[nx, ny] != null)
            {
                toDestroy.Add(grid[nx, ny]);
                Debug.Log($"[QUEEN] Sol alt çapraz: ({nx}, {ny})");
            }
        }

        Debug.Log($"[QUEEN POWER] Toplam {toDestroy.Count} taş yok edilecek!");

        // 💥 EFEFEKT VE YOK ETME
        foreach (ChessPiece piece in toDestroy)
        {
            if (piece == null) continue;

            // Efekt
            if (matchEffectPrefab != null)
            {
                ParticleSystem fx = Instantiate(
                    matchEffectPrefab,
                    piece.transform.position,
                    Quaternion.identity
                );
                Destroy(fx.gameObject, 2f);
            }

            // Grid'den çıkar
            if (grid[piece.x, piece.y] == piece)
            {
                grid[piece.x, piece.y] = null;
            }

            // Yok et
            Destroy(piece.gameObject);

            if (missionManager != null) missionManager.OnPowerUpUsed();

            yield return new WaitForSeconds(0.03f); // Daha hızlı dalga efekti
        }

        // Queen'i de yok et
        if (grid[qx, qy] == queen)
        {
            grid[qx, qy] = null;
            Destroy(queen.gameObject);
        }

        yield return new WaitForSeconds(0.2f);
    }
    IEnumerator ActivateKingPower(ChessPiece king)
    {
        // Daha güvenli null kontrolü
        if (king == null || king.gameObject == null)
        {
            Debug.LogError("[KING POWER] King objesi null!");
            yield break;
        }

        // Koordinatları hemen al
        int kx = king.x;
        int ky = king.y;

        // King'in hala grid'de olup olmadığını kontrol et
        if (kx < 0 || kx >= width || ky < 0 || ky >= height || grid[kx, ky] != king)
        {
            Debug.LogError($"[KING POWER] King grid'de değil veya pozisyonu hatalı: ({kx}, {ky})");
            yield break;
        }

        List<ChessPiece> toDestroy = new List<ChessPiece>();

        Debug.Log($"[KING POWER] Şah pozisyonu: ({kx}, {ky})");

        // 🔊 SES (Kendi ses efekti)
        if (kingSound != null)
            AudioSource.PlayClipAtPoint(kingSound, Camera.main.transform.position);

        // ==========================================
        // 4x4 ALAN PATLAMASI
        // King'in etrafında 4x4 alan yok et
        // ==========================================

        int startX = kx - 1; // King'in solundan başla
        int startY = ky - 1; // King'in altından başla

        // Alanın grid sınırları içinde kalmasını sağla
        startX = Mathf.Clamp(startX, 0, width - 4);
        startY = Mathf.Clamp(startY, 0, height - 4);

        Debug.Log($"[KING] 4x4 patlama alanı: Başlangıç ({startX},{startY})");

        // 4x4 alanındaki tüm taşları topla
        for (int x = startX; x < startX + 4 && x < width; x++)
        {
            for (int y = startY; y < startY + 4 && y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    toDestroy.Add(grid[x, y]);
                    Debug.Log($"[KING] Patlama alanına eklendi: ({x}, {y})");
                }
            }
        }

        Debug.Log($"[KING POWER] Toplam {toDestroy.Count} taş yok edilecek!");

        // 4x4 alanın merkezini bul (efekt için)
        float spacedCellSize = cellSize * spacingMultiplier;
        Vector3 centerPos = new Vector3(
            (startX + 1.5f) * spacedCellSize,
            (startY + 1.5f) * spacedCellSize,
            0
        );
        Vector3 worldCenterPos = gridOrigin.TransformPoint(centerPos);

        Debug.Log($"[KING] Patlama merkezi: {worldCenterPos}");

        // Patlama efekti
        if (kingCenterExplosionPrefab != null)
        {
            GameObject effect = Instantiate(
                kingCenterExplosionPrefab,
                worldCenterPos,
                Quaternion.identity
            );
            Destroy(effect, 3f);
        }

        List<ChessPiece> corners = new List<ChessPiece>();
        List<ChessPiece> edges = new List<ChessPiece>();
        List<ChessPiece> inner = new List<ChessPiece>();

        foreach (ChessPiece piece in toDestroy)
        {
            if (piece == null || piece.gameObject == null) continue;

            // King'i listeden çıkar (kendisini yok etmeyelim)
            if (piece == king) continue;

            int relX = piece.x - startX;
            int relY = piece.y - startY;

            if ((relX == 0 || relX == 3) && (relY == 0 || relY == 3))
                corners.Add(piece);
            else if (relX == 0 || relX == 3 || relY == 0 || relY == 3)
                edges.Add(piece);
            else
                inner.Add(piece);
        }

        // 1. Önce köşeler patlasın
        foreach (ChessPiece piece in corners)
        {
            DestroyPieceWithEffect(piece);
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.1f);

        // 2. Sonra kenarlar patlasın
        foreach (ChessPiece piece in edges)
        {
            DestroyPieceWithEffect(piece);
            yield return new WaitForSeconds(0.03f);
        }

        yield return new WaitForSeconds(0.1f);

        // 3. En son iç kısım patlasın
        foreach (ChessPiece piece in inner)
        {
            DestroyPieceWithEffect(piece);
            yield return new WaitForSeconds(0.02f);
        }

        // King'i SON olarak yok et
        if (king != null && king.gameObject != null)
        {
            if (kx >= 0 && kx < width && ky >= 0 && ky < height)
            {
                if (grid[kx, ky] == king)
                {
                    grid[kx, ky] = null;
                }
            }

            // King için özel efekt (isteğe bağlı)
            if (matchEffectPrefab != null)
            {
                ParticleSystem fx = Instantiate(
                    matchEffectPrefab,
                    king.transform.position,
                    Quaternion.identity
                );
                Destroy(fx.gameObject, 2f);
            }

            Destroy(king.gameObject);
            Debug.Log("[KING POWER] King yok edildi.");
        }

        if (missionManager != null) missionManager.OnPowerUpUsed();

        yield return new WaitForSeconds(0.3f);
    }
    void DestroyPieceWithEffect(ChessPiece piece)
    {
        if (piece == null || piece.gameObject == null) return;

        // Grid'den çıkar (önce grid'i kontrol et)
        if (piece.x >= 0 && piece.x < width && piece.y >= 0 && piece.y < height)
        {
            if (grid[piece.x, piece.y] == piece)
            {
                grid[piece.x, piece.y] = null;
            }
        }

        // Standart efekt
        if (matchEffectPrefab != null)
        {
            ParticleSystem fx = Instantiate(
                matchEffectPrefab,
                piece.transform.position,
                Quaternion.identity
            );
            Destroy(fx.gameObject, 2f);
        }

        // Yok et
        Destroy(piece.gameObject);
    }


    public void IncreaseMoveCount(int amount)
    {
        moveCountLeft += amount;
        UpdateUI();
    }

    public void OnPowerUpUsed()
    {
        if (missionManager != null) missionManager.OnPowerUpUsed();
    }
}