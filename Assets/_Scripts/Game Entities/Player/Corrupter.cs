using System.Collections.Generic;
using UnityEngine;
using Xonix.Grid;
using System;



namespace Xonix.Entities.Player
{
    using static StaticData;

    /// <summary>
    /// Marks zone without enemy by the 
    /// </summary>
    public class Corrupter
    {
        private readonly GridNodeSource _corruptedNodeSource;
        private readonly GridNodeSource _nonCorruptedNodeSource;

        private readonly Vector2[] _neighboursTemplates = new Vector2[4] // Convenient node neighbours position
        {
            new Vector2(0f, CellSize),
            new Vector2(0f, -CellSize),
            new Vector2(CellSize, 0f),
            new Vector2(-CellSize, 0f),
        };



        public Corrupter(GridNodeSource corruptedNodeSource, GridNodeSource nonCorruptedNodeSource)
        {
            _corruptedNodeSource = corruptedNodeSource;
            _nonCorruptedNodeSource = nonCorruptedNodeSource;
        }



        /// <summary>
        /// Mark closed with corrupted or grid border zone with corruption source
        /// </summary>
        /// <param name="seedNodePosition">Corruption start node</param>
        /// <param name="checkedPositions">Set of already checked postions for optimization</param>
        /// <returns>Count of corrupted nodes</returns>
        public IEnumerable<GridNode> CorruptZone(Vector2 seedNodePosition, ISet<Vector2> checkedPositions)
        {
            var uncheckedNodes = new Stack<GridNode>();
            var corruptedNodes = new HashSet<GridNode>();

            var seedNode = GetNode(seedNodePosition);
            uncheckedNodes.Push(seedNode);

            Action onZoneFreeOfEnemies = null;
            int corruptedNodeCount = 0;

            while (uncheckedNodes.Count != 0)
            {
                var currentPickedNode = uncheckedNodes.Pop();

                if (checkedPositions.Contains(currentPickedNode.Position) ||
                    currentPickedNode.State == _corruptedNodeSource.State)
                    continue;

                checkedPositions.Add(currentPickedNode.Position);

                if (currentPickedNode.State == _nonCorruptedNodeSource.State)
                {
                    onZoneFreeOfEnemies += () => CorruptNode(currentPickedNode);
                    corruptedNodes.Add(currentPickedNode);
                    corruptedNodeCount++;
                }

                foreach (var neighbourTemplate in _neighboursTemplates)
                    uncheckedNodes.Push(GetNode(currentPickedNode.Position + neighbourTemplate));
            }

            if (IsZoneFreeOfEnemies(checkedPositions))
            {
                onZoneFreeOfEnemies?.Invoke();
                return corruptedNodes;
            }

            return new HashSet<GridNode>();
        }

        /// <summary>
        /// Immidietry corruptes all nodes in collection
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns>Count of corrupted nodes</returns>
        public int CorruptNodes(IEnumerable<GridNode> nodes)
        {
            int nodesCount = 0;

            foreach (var node in nodes)
            {
                CorruptNode(node);
                nodesCount++;
            }

            return nodesCount;
        }

        private void CorruptNode(GridNode node) => node.SetSource(_corruptedNodeSource);

        /// <summary>
        /// Decorrupts all nodes in collection
        /// </summary>
        /// <param name="nodes"></param>
        public void ReleaseNodes(IEnumerable<GridNode> nodes)
        {
            foreach (var node in nodes)
                node.SetSource(_nonCorruptedNodeSource);
        }

        private bool IsZoneFreeOfEnemies(ISet<Vector2> zoneNodesPositions)
        {
            var nodesCount = zoneNodesPositions.Count;
            var enemiesPositions = GetEnemiesPositions();

            zoneNodesPositions.ExceptWith(enemiesPositions);

            return zoneNodesPositions.Count == nodesCount; // If not the same count of positions - enemies stay in the zone
        }

        /// <summary>
        /// Gets all positions of enemies, that hypothetically can be in the zone
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Vector2> GetEnemiesPositions()
        {
            foreach (var enemy in XonixGame.SeaEnemies)
                yield return enemy.Position;
        }

        private GridNode GetNode(Vector2 vector2)
        {
            if (!XonixGame.TryToGetNodeWithPosition(vector2, out GridNode node))
                throw new UnityException($"Node with position {vector2} doesn't exist");

            return node;
        }
    }
}
