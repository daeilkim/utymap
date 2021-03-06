﻿using System.Collections.Generic;
using UtyMap.Unity.Core;
using UtyMap.Unity.Core.Tiling;
using UtyMap.Unity.Core.Utils;
using Moq;
using NUnit.Framework;
using UnityEngine;
using UtyDepend.Config;
using UtyMap.Unity.Infrastructure.Primitives;
using UtyMap.Unity.Tests.Helpers;
using UtyRx;

namespace UtyMap.Unity.Tests.Core.Tiling
{
    [TestFixture]
    public class TileControllerTests
    {
        private const int LevelOfDetails = 15;
        private readonly GeoCoordinate _worldZeroPoint = TestHelper.WorldZeroPoint;

        private TileController _tileController;
        private Mock<IObserver<Tile>> _tileObserver;
        private Mock<IConfigSection> _configSection;
        
        [SetUp]
        public void Setup()
        {
            _configSection = new Mock<IConfigSection>();
            _configSection.Setup(c => c.GetFloat(@"tile/sensitivity", It.IsAny<float>())).Returns(20);
            _configSection.Setup(c => c.GetFloat(@"tile/offset", It.IsAny<float>())).Returns(100);

            _tileObserver = new Mock<IObserver<Tile>>();

            _tileController = new TileController();
            _tileController.Projection = new CartesianProjection(_worldZeroPoint);
            _tileController.Configure(_configSection.Object);
            _tileController.Subscribe(_tileObserver.Object);
        }

        [Test(Description = "Tests whether first tile can be loaded (done in test setup).")]
        public void CanOnPositionLoadFirstTile()
        {
            QuadKey quadKey = GeoUtils.CreateQuadKey(_worldZeroPoint, LevelOfDetails);

            _tileController.OnPosition(_worldZeroPoint, LevelOfDetails);

            _tileObserver.Verify(o => o.OnNext(It.Is<Tile>(tile => CheckQuadKey(tile.QuadKey, quadKey))));
        }

        [Test(Description = "Tests whether next tile can be loaded when position is changed.")]
        public void CanOnPositionLoadNextNorthTile()
        {
            var newQuadKey = new QuadKey(17602, 10743, LevelOfDetails);
            _tileController.OnPosition(_worldZeroPoint, LevelOfDetails);
            _tileObserver.ResetCalls();

            _tileController.OnPosition(MovePosition(_worldZeroPoint, new Vector2(0, 1), 400), LevelOfDetails);

            _tileObserver.Verify(o => o.OnNext(It.Is<Tile>(tile => CheckQuadKey(tile.QuadKey, newQuadKey))));
        }

        [Test (Description = "Tests whether far tile can be disposed.")]
        public void CanOnPositionUnloadFarTile()
        {
            _configSection.Setup(c => c.GetInt(@"tile/max_tile_distance", It.IsAny<int>())).Returns(1);
            QuadKey quadKey = GeoUtils.CreateQuadKey(_worldZeroPoint, LevelOfDetails);

            for (int i = 0; i < 2; ++i)
                _tileController.OnPosition(MovePosition(_worldZeroPoint, new Vector2(1, 0), i*400), LevelOfDetails);

            _tileObserver.Verify(o => o.OnNext(It.Is<Tile>(tile => tile.IsDisposed && CheckQuadKey(tile.QuadKey, quadKey))));
        }

        [Test(Description = "Tests whether 2x2 tile grid can be loaded from rectangle.")]
        public void CanOnRegionLoadMultipleTiles()
        {
            var rectangle = new Rectangle(0, 0, 1000, 1000);

            _tileController.OnRegion(rectangle, LevelOfDetails);

            _tileObserver.Verify(o => o.OnNext(It.IsAny<Tile>()), Times.Exactly(4));
        }

        [Test(Description = "Tests whether 3x3 tile grid can be loaded from rectangle in spiral order.")]
        public void CanOnRegionLoadMultipleTilesInSpiralOrder()
        {
            var result = new List<Tile>();
            var rectangle = new Rectangle(0, 0, 800, 600);
            _tileController.Subscribe(result.Add);

            _tileController.OnRegion(rectangle, 16);
           
            int xBase = 35200, yBase = 21400;
            var expectedOrder = new List<Tuple<int,int>>()
            {
               new Tuple<int, int>(06, 88), new Tuple<int, int>(07, 88), new Tuple<int, int>(07, 87),
               new Tuple<int, int>(06, 87), new Tuple<int, int>(05, 87), new Tuple<int, int>(05, 88),
               new Tuple<int, int>(05, 89), new Tuple<int, int>(06, 89), new Tuple<int, int>(07, 89)
            };
            Assert.AreEqual(expectedOrder.Count, result.Count);
            for (int i=0; i < expectedOrder.Count; ++i)
                Assert.AreEqual(new QuadKey(xBase + expectedOrder[i].Item1,
                                            yBase + expectedOrder[i].Item2, 16), 
                                result[i].QuadKey);
        }

        #region Helpers

        private GeoCoordinate MovePosition(GeoCoordinate currentPosition, Vector2 direction, float distance)
        {
            var point = GeoUtils.ToMapCoordinate(_worldZeroPoint, currentPosition);
            var newPoint = point + direction*distance;
            return GeoUtils.ToGeoCoordinate(_worldZeroPoint, newPoint);
        }

        private bool CheckQuadKey(QuadKey actual, QuadKey expected)
        {
            Assert.AreEqual(expected.LevelOfDetail, actual.LevelOfDetail);
            Assert.AreEqual(expected.TileX, actual.TileX);
            Assert.AreEqual(expected.TileY, actual.TileY);
            return true;
        }

        #endregion
    }
}