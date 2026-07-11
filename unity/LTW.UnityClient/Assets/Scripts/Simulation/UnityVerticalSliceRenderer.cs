using System.Collections.Generic;
using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnityVerticalSliceRenderer : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();

        private void Update()
        {
            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            RenderLane();
            foreach (var tower in snapshot.Towers)
            {
                RenderObject($"tower-{tower.EntityId.Value}", tower.Position, Color.blue, 0.85f);
            }

            foreach (var creep in snapshot.Creeps)
            {
                RenderObject($"creep-{creep.EntityId.Value}", creep.Position, Color.red, 0.55f);
            }
        }

        private void RenderLane()
        {
            for (var x = 0; x < 6; x++)
            {
                RenderObject($"cell-{x}", new GridPosition(x, 1), Color.gray, 0.25f);
            }
        }

        private void RenderObject(string key, GridPosition position, Color color, float scale)
        {
            if (!objects.TryGetValue(key, out var instance))
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = key;
                objects[key] = instance;
            }

            instance.transform.position = new Vector3(position.X, 0f, position.Y);
            instance.transform.localScale = new Vector3(scale, scale, scale);
            var renderer = instance.GetComponent<Renderer>();
            if (renderer is not null)
            {
                renderer.material.color = color;
            }
        }
    }
}
