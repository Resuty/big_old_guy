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

        private void Start()
        {
            gameObject.AddComponent<CardDuel.Learning.TeachingGameScreen>();
        }

        public void Restart(int newSeed)
        {
            seed = newSeed;
            Engine = new GameEngine(seed);
        }
    }
}