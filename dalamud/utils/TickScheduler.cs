using System.Collections.Generic;

namespace MgAl2O4.Utils
{
    public interface ITickable
    {
        bool Tick(float deltaSeconds);
    }

    public class TickScheduler
    {
        private List<ITickable> tickList = new();

        public void Register(ITickable tickable)
        {
            if (!tickList.Contains(tickable))
            {
                tickList.Add(tickable);
            }
        }

        public void Update(float deltaSeconds)
        {
            for (int idx = tickList.Count - 1; idx >= 0; idx--)
            {
                bool canTick = tickList[idx].Tick(deltaSeconds);
                if (!canTick)
                {
                    tickList.RemoveAt(idx);
                }
            }
        }
    }
}
