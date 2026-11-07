using UnityEngine;

namespace CardDuel
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int seed = 20261022;

        public GameEngine Engine { get; private set; }

        private void Awake()
        {
            Engine = new GameEngine(seed);
        }
    }
}