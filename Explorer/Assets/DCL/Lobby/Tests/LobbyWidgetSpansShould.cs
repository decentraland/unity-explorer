using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyWidgetSpansShould
    {
        private static readonly Regex SPAN_SUFFIX = new (@"(\d+)By(\d+)$");

        [Test]
        public void DeclareTheSpanEncodedInEveryName()
        {
            foreach (LobbyWidgetId widgetId in Enum.GetValues(typeof(LobbyWidgetId)))
            {
                //Arrange
                Match match = SPAN_SUFFIX.Match(widgetId.ToString());
                Assert.IsTrue(match.Success, $"{widgetId} does not end with its span, columns x rows");
                var encoded = new Vector2Int(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));

                //Act
                Vector2Int declared = widgetId.Span();

                //Assert
                Assert.AreEqual(encoded, declared, widgetId.ToString());
            }
        }

        [Test]
        public void RejectAnUndeclaredWidget()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((LobbyWidgetId)999).Span());
        }
    }
}
