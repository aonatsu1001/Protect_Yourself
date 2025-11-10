using UnityEngine;

[CreateAssetMenu(fileName = "SurvivalConfig", menuName = "Disaster/Survival Config")]
public class SurvivalConfig : ScriptableObject
{
    [Header("初期ストック（家庭全体）")]
    [Tooltip("食料（食事数換算。例: 1=一食分）")]
    public int initialFoodMeals = 18;        // 例: 3人 * 2食 * 3日
    [Tooltip("飲料水（リットル）。例: 1=1L")]
    public float initialWaterLiters = 18f;   // 例: 3人 * 3L/日 * 2日

    [Header("1日あたり必要量（1人）")]
    [Tooltip("食事数/日/人")]
    public int mealsPerDayPerPerson = 2;
    [Tooltip("飲料水L/日/人（飲用＋簡易調理）")]
    public float waterLitersPerDayPerPerson = 3f;

    [Header("世帯人数")]
    public int people = 3;

    public int MealsNeededPerDay() => mealsPerDayPerPerson * people;
    public float WaterNeededPerDay() => waterLitersPerDayPerPerson * people;
}
