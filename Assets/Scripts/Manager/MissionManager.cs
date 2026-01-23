using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MissionManager : MonoBehaviour
{
    [System.Serializable]
    public class Mission
    {
        public enum MissionType
        {
            CreateMultipleKnights,
            CreateMultipleRooks,
            CreateMultipleBishops,
            CreateMultipleQueens,
            CreateMultipleKings,
            CreateThreeSpecials,
            ClearBoard,
            Combo,
            UsePowerUp
        }

        public MissionType missionType;
        public int targetCount = 1;
        public int currentCount = 0;
        public bool isCompleted = false;

        public string GetMissionText()
        {
            switch (missionType)
            {
                case MissionType.CreateMultipleKnights:
                    return $"{targetCount}x Knight";
                case MissionType.CreateMultipleRooks:
                    return $"{targetCount}x Rook";
                case MissionType.CreateMultipleBishops:
                    return $"{targetCount}x Bishop";
                case MissionType.CreateMultipleQueens:
                    return $"{targetCount}x Queen";
                case MissionType.CreateMultipleKings:
                    return $"{targetCount}x King";
                case MissionType.CreateThreeSpecials:
                    return $"3x Different Special";
                case MissionType.ClearBoard:
                    return $"Clear {targetCount} Pieces";
                case MissionType.Combo:
                    return $"{targetCount}x Combo";
                case MissionType.UsePowerUp:
                    return $"Use {targetCount}x Power-up";
                default:
                    return "Unknown";
            }
        }
    }

    [Header("UI References")]
    public TextMeshProUGUI missionText1;
    public TextMeshProUGUI missionText2;
    public TextMeshProUGUI levelText;

    [Header("Level Complete")]
    public ParticleSystem levelCompleteEffect;
    public GameObject winPanel;
    public AudioClip winSound;
    public AudioClip fireworksSound;

    [Header("Mission Settings")]
    public List<Mission> currentMissions = new List<Mission>();

    // İstatistikler
    private static int savedLevel = 1;
    private int knightsCreated = 0;
    private int rooksCreated = 0;
    private int bishopsCreated = 0;
    private int queensCreated = 0;
    private int kingsCreated = 0;
    private int clearedPieces = 0;
    private int comboCount = 0;
    private int powerUpsUsed = 0;

    private int currentLevel = 1;
    private bool isLevelCompleting = false;


    void Start()
    {
        currentLevel = savedLevel;
        UpdateLevelUI();
        GenerateMissions();
        UpdateUI();
    }

    void LoadLevel()
    {
        UpdateLevelUI();
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    void GenerateMissions()
    {
        currentMissions.Clear();
        ResetStats();

        // Her zaman 2 görev
        currentMissions.Add(CreateMission(0));
        currentMissions.Add(CreateMission(1));
    }

    Mission CreateMission(int index)
    {
        Mission mission = new Mission();

        // İlk görev daha kolay, ikinci görev daha zor
        if (index == 0)
        {
            // Kolay görevler
            int missionTypeIndex = Random.Range(0, 4);
            mission.missionType = (Mission.MissionType)missionTypeIndex;
            switch (mission.missionType)
            {
                case Mission.MissionType.CreateMultipleKnights:
                    mission.targetCount = Random.Range(2, 5);
                    break;
                case Mission.MissionType.CreateMultipleRooks:
                    mission.targetCount = Random.Range(1, 3);
                    break;
                case Mission.MissionType.ClearBoard:
                    mission.targetCount = Random.Range(20, 40);
                    break;
                case Mission.MissionType.UsePowerUp:
                    mission.targetCount = Random.Range(1, 3);
                    break;
            }
        }
        else
        {
            // Zor görevler
            int missionTypeIndex = Random.Range(4, 8);
            mission.missionType = (Mission.MissionType)missionTypeIndex;
            switch (mission.missionType)
            {
                case Mission.MissionType.CreateMultipleBishops:
                    mission.targetCount = Random.Range(1, 3);
                    break;
                case Mission.MissionType.CreateMultipleQueens:
                    mission.targetCount = 1;
                    break;
                case Mission.MissionType.CreateMultipleKings:
                    mission.targetCount = 1;
                    break;
                case Mission.MissionType.CreateThreeSpecials:
                    mission.targetCount = 3;
                    break;
                case Mission.MissionType.Combo:
                    mission.targetCount = Random.Range(2, 4);
                    break;
            }
        }

        return mission;
    }

    void ResetStats()
    {
        knightsCreated = 0;
        rooksCreated = 0;
        bishopsCreated = 0;
        queensCreated = 0;
        kingsCreated = 0;
        clearedPieces = 0;
        comboCount = 0;
        powerUpsUsed = 0;
        isLevelCompleting = false;
    }

    // PuzzleManager event'leri
    public void OnKnightCreated()
    {
        knightsCreated++;
        CheckMissions();
        UpdateUI();
    }

    public void OnRookCreated()
    {
        rooksCreated++;
        CheckMissions();
        UpdateUI();
    }

    public void OnBishopCreated()
    {
        bishopsCreated++;
        CheckMissions();
        UpdateUI();
    }

    public void OnQueenCreated()
    {
        queensCreated++;
        CheckMissions();
        UpdateUI();
    }

    public void OnKingCreated()
    {
        kingsCreated++;
        CheckMissions();
        UpdateUI();
    }

    public void OnPiecesCleared(int count)
    {
        clearedPieces += count;
        CheckMissions();
        UpdateUI();
    }

    public void OnComboPerformed(int comboLength)
    {
        if (comboLength > comboCount)
        {
            comboCount = comboLength;
            CheckMissions();
            UpdateUI();
        }
    }

    public void OnPowerUpUsed()
    {
        powerUpsUsed++;
        CheckMissions();
        UpdateUI();
    }

    void CheckMissions()
    {
        if (isLevelCompleting) return;

        bool allCompleted = true;

        foreach (Mission mission in currentMissions)
        {
            if (mission.isCompleted) continue;

            UpdateProgress(mission);

            if (mission.currentCount >= mission.targetCount)
            {
                mission.isCompleted = true;
                mission.currentCount = mission.targetCount;
            }
            else
            {
                allCompleted = false;
            }
        }

        if (allCompleted && currentMissions.Count > 0)
        {
            isLevelCompleting = true;
            StartCoroutine(CompleteLevel());
        }
    }

    void UpdateProgress(Mission mission)
    {
        switch (mission.missionType)
        {
            case Mission.MissionType.CreateMultipleKnights:
                mission.currentCount = knightsCreated;
                break;

            case Mission.MissionType.CreateMultipleRooks:
                mission.currentCount = rooksCreated;
                break;

            case Mission.MissionType.CreateMultipleBishops:
                mission.currentCount = bishopsCreated;
                break;

            case Mission.MissionType.CreateMultipleQueens:
                mission.currentCount = queensCreated;
                break;

            case Mission.MissionType.CreateMultipleKings:
                mission.currentCount = kingsCreated;
                break;

            case Mission.MissionType.CreateThreeSpecials:
                int specialTypes = 0;
                if (knightsCreated > 0) specialTypes++;
                if (rooksCreated > 0) specialTypes++;
                if (bishopsCreated > 0) specialTypes++;
                if (queensCreated > 0) specialTypes++;
                if (kingsCreated > 0) specialTypes++;
                mission.currentCount = Mathf.Min(specialTypes, 3);
                break;

            case Mission.MissionType.ClearBoard:
                mission.currentCount = clearedPieces;
                break;

            case Mission.MissionType.Combo:
                mission.currentCount = comboCount;
                break;

            case Mission.MissionType.UsePowerUp:
                mission.currentCount = powerUpsUsed;
                break;
        }
    }

    System.Collections.IEnumerator CompleteLevel()
    {
        Debug.Log($"LEVEL {currentLevel} COMPLETED!");

        if (winSound != null)
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);

        if (fireworksSound != null)
            AudioSource.PlayClipAtPoint(fireworksSound, Camera.main.transform.position);

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        if (levelCompleteEffect != null)
        {
            levelCompleteEffect.Play();
        }
        else
        {
            Debug.LogWarning("Level Complete Effect atanmamış!");
        }

        yield return new WaitForSeconds(5f);

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        NextLevel();
    }

    void NextLevel()
    {
        currentLevel++;
        savedLevel = currentLevel;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void UpdateUI()
    {
        if (missionText1 != null && currentMissions.Count > 0)
        {
            Mission m1 = currentMissions[0];
            missionText1.text = $"{m1.GetMissionText()} ({m1.currentCount}/{m1.targetCount})";
            missionText1.color = m1.isCompleted ? Color.green : Color.white;
        }

        if (missionText2 != null && currentMissions.Count > 1)
        {
            Mission m2 = currentMissions[1];
            missionText2.text = $"{m2.GetMissionText()} ({m2.currentCount}/{m2.targetCount})";
            missionText2.color = m2.isCompleted ? Color.green : Color.white;
        }

        UpdateLevelUI();
    }
    public void SetWinPanel(GameObject panel)
    {
        winPanel = panel;
    }
    void UpdateLevelUI()
    {
        if (levelText != null)
        {
            levelText.text = $"{currentLevel}";
        }
    }
}