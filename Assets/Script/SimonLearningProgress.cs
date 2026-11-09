using System;

[Serializable]
public class SimonLearningProgress
{
    public int memorySpan = 2;
    public int memoryColorCount = 4;
    public int bestUnaidedSpan;
    public int memorySuccessStreak;
    public int memoryDifficultyStreak;
    public float rhythmBpm = 40f;
    public bool rhythmSilentBeatsEnabled;
    public bool rhythmExtendedMeasuresEnabled;
    public int rhythmSuccessStreak;
    public int rhythmDifficultyStreak;

    public void RecordMemoryRound(bool success)
    {
        if (success)
        {
            bestUnaidedSpan = Math.Max(bestUnaidedSpan, memorySpan);
            memorySuccessStreak++;
            memoryDifficultyStreak = 0;
            if (memorySuccessStreak >= 3)
            {
                if (memoryColorCount == 4 && memorySpan < 4)
                    memorySpan++;
                else if (memoryColorCount == 4)
                    memoryColorCount = 5;
                else
                    memorySpan = Math.Min(6, memorySpan + 1);
                memorySuccessStreak = 0;
            }
        }
        else
        {
            memorySuccessStreak = 0;
            if (++memoryDifficultyStreak >= 2)
            {
                if (memoryColorCount > 4 && memorySpan > 4)
                    memorySpan--;
                else if (memoryColorCount > 4)
                    memoryColorCount = 4;
                else
                    memorySpan = Math.Max(2, memorySpan - 1);
                memoryDifficultyStreak = 0;
            }
        }
    }

    public void RecordRhythmRound(bool success, float minBpm = 40f, float maxBpm = 90f)
    {
        if (success)
        {
            rhythmSuccessStreak++;
            rhythmDifficultyStreak = 0;
            if (rhythmSuccessStreak >= 3)
            {
                if (rhythmBpm < maxBpm)
                    rhythmBpm = Math.Min(maxBpm, rhythmBpm + 5f);
                else if (!rhythmSilentBeatsEnabled)
                    rhythmSilentBeatsEnabled = true;
                else
                    rhythmExtendedMeasuresEnabled = true;
                rhythmSuccessStreak = 0;
            }
        }
        else
        {
            rhythmSuccessStreak = 0;
            if (++rhythmDifficultyStreak >= 2)
            {
                if (rhythmExtendedMeasuresEnabled)
                    rhythmExtendedMeasuresEnabled = false;
                else if (rhythmSilentBeatsEnabled)
                    rhythmSilentBeatsEnabled = false;
                else
                    rhythmBpm = Math.Max(minBpm, rhythmBpm - 5f);
                rhythmDifficultyStreak = 0;
            }
        }
    }
}
