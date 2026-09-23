using System;

/// <summary>Regras da sessão, independentes da interface e dos serviços do aplicativo.</summary>
public sealed class MinigameSessionState
{
    public enum Phase { Preparing, Playing, BetweenTurns, Completed, Abandoned }

    private readonly int[] ordersPerTurn;
    private readonly bool[] resolvedOrders;
    private int resolvedThisTurn;

    public Phase CurrentPhase { get; private set; }
    public int TurnIndex { get; private set; }
    public int TurnCount => ordersPerTurn.Length;
    public int OrderCount => ordersPerTurn[TurnIndex];
    public int Delivered { get; private set; }
    public int Missed { get; private set; }
    public int ResolvedThisTurn => resolvedThisTurn;

    public MinigameSessionState(int[] counts)
    {
        if (counts == null || counts.Length != 3)
            throw new ArgumentException("Configure exatamente três turnos.", nameof(counts));

        int capacity = 0;
        foreach (int count in counts)
        {
            if (count < 1)
                throw new ArgumentException("Cada turno precisa de pelo menos um pedido.", nameof(counts));
            capacity = Math.Max(capacity, count);
        }

        ordersPerTurn = (int[])counts.Clone();
        resolvedOrders = new bool[capacity];
        CurrentPhase = Phase.Preparing;
    }

    public bool StartTurn()
    {
        if (CurrentPhase != Phase.Preparing)
            return false;
        CurrentPhase = Phase.Playing;
        return true;
    }

    /// <summary>O índice do turno impede callbacks atrasados de resolver pedidos do próximo lote.</summary>
    public bool ResolveOrder(int turnIndex, int orderIndex, bool delivered)
    {
        if (CurrentPhase != Phase.Playing || turnIndex != TurnIndex
            || orderIndex < 0 || orderIndex >= OrderCount || resolvedOrders[orderIndex])
            return false;

        resolvedOrders[orderIndex] = true;
        if (delivered) Delivered++;
        else Missed++;

        if (++resolvedThisTurn == OrderCount)
            CurrentPhase = TurnIndex + 1 == TurnCount ? Phase.Completed : Phase.BetweenTurns;
        return true;
    }

    public bool PrepareNextTurn()
    {
        if (CurrentPhase != Phase.BetweenTurns)
            return false;
        TurnIndex++;
        resolvedThisTurn = 0;
        Array.Clear(resolvedOrders, 0, resolvedOrders.Length);
        CurrentPhase = Phase.Preparing;
        return true;
    }

    public bool Abandon()
    {
        if (CurrentPhase == Phase.Completed || CurrentPhase == Phase.Abandoned)
            return false;
        CurrentPhase = Phase.Abandoned;
        return true;
    }
}
