using UnityEngine;

[System.Serializable]
public class PlayerState
{
    public int dayElapsed;           // 経過日数
    public int foodMeals;            // 残り食事数
    public float waterLiters;        // 残り水(L)
    public bool alive = true;

    public PlayerState(int foodMeals, float waterLiters)
    {
        this.foodMeals = foodMeals;
        this.waterLiters = waterLiters;
        this.dayElapsed = 0;
        this.alive = true;
    }

    /// 1日分を消費して状態更新
    public void ConsumeOneDay(SurvivalConfig cfg)
    {
        if (!alive) return;

        dayElapsed++;

        foodMeals -= cfg.MealsNeededPerDay();
        waterLiters -= cfg.WaterNeededPerDay();

        if (foodMeals < 0) foodMeals = 0;
        if (waterLiters < 0f) waterLiters = 0f;

        // 簡易な生存条件：どちらかゼロが続いたら危険（ここは後で厳密化OK）
        if (foodMeals == 0 || waterLiters == 0f)
        {
            // 例：2日連続で欠乏したら死亡判定…など後でルール調整
            // ここではサンプルとして欠乏した瞬間にアウトにする
            alive = false;
        }
    }

    public string Summary()
    {
        return $"Day {dayElapsed}\n" +
               $"残フード: {foodMeals} 食分 / 残水: {waterLiters:0.0} L\n" +
               $"生存: {(alive ? "○" : "×")}";
    }
}
