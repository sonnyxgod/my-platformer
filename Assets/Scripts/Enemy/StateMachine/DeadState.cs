public class DeadState : IState
{
    private readonly EnemyController ctx;

    public DeadState(EnemyController ctx) => this.ctx = ctx;

    public void Enter()
    {
        ctx.Stop();
        // Хітбокс вимикається, тож без цього тіло провалилося б крізь підлогу
        ctx.Body.simulated = false;
        ctx.Animator.SetTrigger("Die");
    }

    public void Tick() { } // кінцевий стан: виходу немає і не має бути

    public void Exit() { }
}
