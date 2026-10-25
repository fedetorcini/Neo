public interface IDemoDamageable
{
    float CurrentHealth { get; }
    float MaximumHealth { get; }
    void TakeDamage(float amount);
}
