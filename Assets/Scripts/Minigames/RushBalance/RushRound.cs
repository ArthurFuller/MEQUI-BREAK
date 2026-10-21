using System;

/// <summary>Fila reciclável do Rush Balance, sem dependência da Unity.</summary>
public sealed class RushRound
{
    public enum Status { Hidden, Waiting, Queued, Ready, Delivered, Missed }

    public struct Order
    {
        public Status State;
        public int Customer, Items;
        public float Remaining, Patience, Work, Duration;
        public bool Arriving;
    }

    public readonly Order[] Orders;
    private readonly int[] queue;
    public int QueueCount { get; private set; }
    public event Action<int, bool> Resolved;

    public RushRound(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Orders = new Order[capacity];
        queue = new int[capacity];
    }

    public void Reset()
    {
        Array.Clear(Orders, 0, Orders.Length);
        Array.Clear(queue, 0, queue.Length);
        QueueCount = 0;
    }

    public bool Activate(int id, int customer, int items, float patience, float duration, bool arriving = false)
    {
        if (id < 0 || id >= Orders.Length || customer < 0 || items < 1 || items > 3)
            return false;

        Status state = Orders[id].State;
        if (state == Status.Waiting || state == Status.Queued || state == Status.Ready)
            return false;

        Orders[id] = new Order
        {
            State = Status.Waiting,
            Customer = customer,
            Arriving = arriving,
            Items = items,
            Patience = Math.Max(.1f, patience),
            Remaining = Math.Max(.1f, patience),
            Duration = Math.Max(.1f, duration)
        };
        return true;
    }

    public bool Enqueue(int id)
    {
        if (id < 0 || id >= Orders.Length || Orders[id].State != Status.Waiting
            || Orders[id].Arriving || QueueCount >= queue.Length) return false;

        Orders[id].State = Status.Queued;
        queue[QueueCount++] = id;
        return true;
    }

    public int Rank(int id)
    {
        for (int i = 0; i < QueueCount; i++)
            if (queue[i] == id) return i;
        return -1;
    }

    public void FinishArrival(int id)
    {
        if (id >= 0 && id < Orders.Length) Orders[id].Arriving = false;
    }

    // Projects FIFO completion with prompt selections/deliveries. Waiting orders
    // also count, so leaving a request unselected does not hide the workload.
    // This is a forecast only: it never changes progress, patience or queue order.
    public float EstimateNewOrderSeconds(float duration, float secondaryRate, float reactionSeconds)
    {
        float rate = Math.Max(0f, Math.Min(1f, secondaryRate));
        float reaction = Math.Max(0f, reactionSeconds);
        float elapsed = 0f;
        float headWork = 0f;
        bool hasHead = false;
        for (int position = 0; position <= QueueCount + Orders.Length; position++)
        {
            float work;
            if (position < QueueCount)
            {
                Order order = Orders[queue[position]];
                work = Math.Max(0f, order.Duration - order.Work);
            }
            else if (position < QueueCount + Orders.Length)
            {
                Order order = Orders[position - QueueCount];
                if (order.State != Status.Waiting) continue;
                work = Math.Max(0f, order.Duration - order.Work);
            }
            else work = Math.Max(.1f, duration);

            if (hasHead)
            {
                elapsed += headWork + reaction;
                work = Math.Max(0f, work - headWork * rate);
            }
            headWork = work;
            hasHead = true;
        }
        return elapsed + headWork + reaction;
    }

    public bool Move(int id, int rank)
    {
        int previous = Rank(id);
        if (previous < 0 || rank < 0 || rank >= QueueCount || previous == rank) return false;

        for (int i = previous; i < rank; i++) queue[i] = queue[i + 1];
        for (int i = previous; i > rank; i--) queue[i] = queue[i - 1];
        queue[rank] = id;
        return true;
    }

    public void Tick(float seconds, float secondaryRate)
    {
        if (seconds <= 0f) return;

        for (int i = 0; i < Orders.Length; i++)
        {
            Status state = Orders[i].State;
            if (state != Status.Waiting && state != Status.Queued && state != Status.Ready) continue;
            if (Orders[i].Arriving) continue;

            Orders[i].Remaining = Math.Max(0f, Orders[i].Remaining - seconds);
            if (Orders[i].Remaining <= 0f) Resolve(i, false);
        }

        // Um pedido pronto ocupa a posição até o toque final.
        int first = QueueCount > 0 ? queue[0] : -1;
        int second = QueueCount > 1 ? queue[1] : -1;
        Advance(first, seconds);
        Advance(second, seconds * Math.Max(0f, Math.Min(1f, secondaryRate)));
    }

    private void Advance(int id, float seconds)
    {
        if (id < 0 || Orders[id].State != Status.Queued) return;
        Orders[id].Work = Math.Min(Orders[id].Duration, Orders[id].Work + seconds);
        if (Orders[id].Work >= Orders[id].Duration)
            Orders[id].State = Status.Ready;
    }

    public bool Deliver(int id)
    {
        if (id < 0 || id >= Orders.Length || Orders[id].State != Status.Ready) return false;
        Resolve(id, true);
        return true;
    }

    public void ResolveRemainingAsMissed()
    {
        for (int i = 0; i < Orders.Length; i++)
        {
            Status state = Orders[i].State;
            if (state == Status.Waiting || state == Status.Queued || state == Status.Ready)
                Resolve(i, false);
        }
    }

    private void Resolve(int id, bool delivered)
    {
        Status state = Orders[id].State;
        if (state == Status.Delivered || state == Status.Missed || state == Status.Hidden) return;

        RemoveFromQueue(id);
        Orders[id].State = delivered ? Status.Delivered : Status.Missed;
        Resolved?.Invoke(id, delivered);
    }

    private void RemoveFromQueue(int id)
    {
        int rank = Rank(id);
        if (rank < 0) return;
        for (int i = rank; i < QueueCount - 1; i++) queue[i] = queue[i + 1];
        QueueCount--;
    }
}
