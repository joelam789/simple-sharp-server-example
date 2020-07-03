using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BigTwoGameLogic
{
    public static class BigTwoLogic
    {
        static readonly List<char> m_RankOrder = new List<char>()
        {
            '3','4','5','6','7','8','9','T','J','Q','K','A','2'
        };

        static readonly List<char> m_SuitOrder = new List<char>()
        {
            'D','C','H','S'
        };

        static readonly Dictionary<string, string> m_CardCodes = new Dictionary<string, string>()
        {
            { "AD", "a" }, { "AC", "b" }, { "AH", "c" }, { "AS", "d" },
            { "2D", "e" }, { "2C", "f" }, { "2H", "g" }, { "2S", "h" },
            { "3D", "i" }, { "3C", "j" }, { "3H", "k" }, { "3S", "l" },
            { "4D", "m" }, { "4C", "n" }, { "4H", "o" }, { "4S", "p" },
            { "5D", "q" }, { "5C", "r" }, { "5H", "s" }, { "5S", "t" },
            { "6D", "u" }, { "6C", "v" }, { "6H", "w" }, { "6S", "x" },
            { "7D", "y" }, { "7C", "z" }, { "7H", "A" }, { "7S", "B" },
            { "8D", "C" }, { "8C", "D" }, { "8H", "E" }, { "8S", "F" },
            { "9D", "G" }, { "9C", "H" }, { "9H", "I" }, { "9S", "J" },
            { "TD", "K" }, { "TC", "L" }, { "TH", "M" }, { "TS", "N" },
            { "JD", "O" }, { "JC", "P" }, { "JH", "Q" }, { "JS", "R" },
            { "QD", "S" }, { "QC", "T" }, { "QH", "U" }, { "QS", "V" },
            { "KD", "W" }, { "KC", "X" }, { "KH", "Y" }, { "KS", "Z" },
        };

        static readonly BigTwoRng m_Rng = new BigTwoRng();

        public static BigTwoRng GetRng()
        {
            return m_Rng;
        }

        public static bool IsValidCard(Card card)
        {
            return m_RankOrder.Contains(card.Rank) && m_SuitOrder.Contains(card.Suit);
        }

        public static string CardToCode(Card card)
        {
            return m_CardCodes[card.ToString()];
        }

        public static Card CodeToCard(string code)
        {
            var str = m_CardCodes.FirstOrDefault(x => x.Value == code).Key;
            if (!string.IsNullOrEmpty(str) && str.Length >= 2)
            {
                return new Card(str[0], str[1]);
            }
            return null;
        }

        public static string EncodeCards(List<Card> cards)
        {
            var strb = new StringBuilder();
            foreach (var card in cards) strb.Append(CardToCode(card));
            return strb.ToString();
        }

        public static List<Card> DecodeCards(string str)
        {
            List<Card> cards = new List<Card>();
            var strb = new StringBuilder();
            foreach (var code in str)
            {
                var card = CodeToCard(code.ToString());
                if (card != null) cards.Add(card);
            }
            return cards;
        }

        public static string ToString(List<Card> cards)
        {
            string str = "";
            for (var i = 0; i < cards.Count; i++)
            {
                if (str.Length <= 0) str = cards[i].ToString();
                else str = str + "," + cards[i].ToString();
            }
            return str;
        }

        public static List<Card> Merge(List<Card> list1, List<Card> list2)
        {
            var i = 0;
            var j = 0;

            var result = new List<Card>();

            while (i < list1.Count && j < list2.Count)
            {
                var card1 = new Card(list1[i]);
                var card2 = new Card(list2[j]);

                var card1Rank = m_RankOrder.IndexOf(card1.Rank);
                var card2Rank = m_RankOrder.IndexOf(card2.Rank);
                if (card1Rank < card2Rank)
                {
                    result.Add(list1[i]);
                    i++;
                }
                else if (card2Rank < card1Rank)
                {
                    result.Add(list2[j]);
                    j++;
                }
                else
                {
                    var card1Suit = m_SuitOrder.IndexOf(card1.Suit);
                    var card2Suit = m_SuitOrder.IndexOf(card2.Suit);
                    if (card1Suit < card2Suit)
                    {
                        result.Add(list1[i]);
                        i++;
                    }
                    else
                    {
                        result.Add(list2[j]);
                        j++;
                    }
                }
            }

            while (i < list1.Count)
            {
                result.Add(list1[i]);
                i++;
            }
            while (j < list2.Count)
            {
                result.Add(list2[j]);
                j++;
            }

            return result;
        }

        public static List<Card> MergeSort(List<Card> cards)
        {
            if (cards.Count <= 1) return cards;

            int middle = cards.Count / 2;
            var left = MergeSort(cards.GetRange(0, middle));
            var right = MergeSort(cards.GetRange(middle, cards.Count - middle));

            return Merge(left, right);
        }

        public static int BinarySearch(List<Card> cards, string card)
        {
            int start = 0;
            int end = cards.Count - 1;
            int middle = (start + end) / 2;

            while (cards[middle].ToString() != card && start <= end)
            {
                // Compare ranks
                int cardFromArrayRank = m_RankOrder.IndexOf(cards[middle].Rank);
                int cardToFindRank = m_RankOrder.IndexOf(card[0]);

                if (cardToFindRank < cardFromArrayRank) end = middle - 1;
                else if (cardToFindRank > cardFromArrayRank) start = middle + 1;
                else
                {
                    int cardFromArraySuit = m_SuitOrder.IndexOf(cards[middle].Suit);
                    int cardToFindSuit = m_SuitOrder.IndexOf(card[1]);

                    if (cardToFindSuit < cardFromArraySuit) end = middle - 1;
                    else start = middle + 1;
                }
                middle = (start + end) / 2;
            }
            if (cards[middle].ToString() == card) return middle;
            return -1;
        }

        public static bool CheckBetterSingle(Card card, Card currentPlay)
        {
            int cardRank = m_RankOrder.IndexOf(card.Rank);
            int currentPlayRank = m_RankOrder.IndexOf(currentPlay.Rank);

            if (cardRank > currentPlayRank) return true;
            else if (currentPlayRank > cardRank) return false;
            else
            {
                int cardSuit = m_SuitOrder.IndexOf(card.Suit);
                int currentPlaySuit = m_SuitOrder.IndexOf(currentPlay.Suit);
                return cardSuit > currentPlaySuit;
            }
        }

        public static bool CheckBetterDouble(List<Card> cards, List<Card> currentPlay)
        {
            if (cards[0].Rank == cards[1].Rank) // pair...
            {
                return CheckBetterSingle(cards[1], currentPlay[1]);
            }
            return false;
        }

        public static bool CheckBetterTriple(List<Card> cards, List<Card> currentPlay)
        {
            if (cards[0].Rank == cards[1].Rank && cards[0].Rank == cards[2].Rank)
            {
                int cardRank = m_RankOrder.IndexOf(cards[0].Rank);
                int currentPlayRank = m_RankOrder.IndexOf(currentPlay[0].Rank);
                return cardRank > currentPlayRank;
            }
            return false;
        }

        public static Tuple<bool, Card> IsStraight(List<Card> cards)
        {
            // need to incorporate A 2 3 4 5 and 2 3 4 5 6 straight
            // need to block out J Q K A 2 straight

            // incorporate A 2 3 4 5 and 2 3 4 5 6 straight
            if (cards[0].Rank == '3' && cards[1].Rank == '4' && cards[2].Rank == '5'
                && (cards[3].Rank == '6' || cards[3].Rank == 'A')
                && cards[4].Rank == '2')
            {
                return Tuple.Create(true, cards[4]);
            }

            // block out J Q K A 2 straight
            if (cards[4].Rank == '2') return Tuple.Create<bool, Card>(false, null);

            // other normal straights...
            int currentRank = m_RankOrder.IndexOf(cards[0].Rank);
            for (int i = 1; i < cards.Count; i++)
            {
                int nextRank = m_RankOrder.IndexOf(cards[i].Rank);
                if (nextRank != currentRank + 1)
                {
                    return Tuple.Create<bool, Card>(false, null);
                }
                currentRank = nextRank;
            }

            return Tuple.Create(true, cards[4]);
        }

        public static Tuple<bool, Card> IsFlush(List<Card> cards)
        {
            var currentSuit = cards[0].Suit;

            for (int i = 1; i < cards.Count; i++)
            {
                if (cards[i].Suit != currentSuit)
                {
                    return Tuple.Create<bool, Card>(false, null);
                }
            }
            return Tuple.Create(true, cards[4]);
        }

        public static Tuple<bool, Card> IsFullHouse(List<Card> cards)
        {
            var counter = new Dictionary<char, int>();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!counter.ContainsKey(cards[i].Rank)) counter.Add(cards[i].Rank, 0);
                counter[cards[i].Rank] = counter[cards[i].Rank] + 1;
            }
            bool foundDouble = false;
            bool foundTriple = false;
            foreach (var item in counter)
            {
                if (counter[item.Key] == 2) foundDouble = true;
                else if (counter[item.Key] == 3) foundTriple = true;
            }
            if (foundDouble && foundTriple) return Tuple.Create(true, cards[2]);
            else return Tuple.Create<bool, Card>(false, null);
        }

        public static Tuple<bool, Card> IsFourOfAKind(List<Card> cards)
        {
            var counter = new Dictionary<char, int>();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!counter.ContainsKey(cards[i].Rank)) counter.Add(cards[i].Rank, 0);
                counter[cards[i].Rank] = counter[cards[i].Rank] + 1;
            }
            foreach (var item in counter)
            {
                if (counter[item.Key] == 4) return Tuple.Create(true, cards[2]);
            }
            return Tuple.Create<bool, Card>(false, null);
        }

        public static bool CheckLegalFiveCardCombo(List<Card> cards)
        {
            return IsFlush(cards).Item1
                || IsStraight(cards).Item1
                || IsFullHouse(cards).Item1
                || IsFourOfAKind(cards).Item1
                ;
        }

        public static Tuple<int, Card> EvaluateFiveCardCombo(List<Card> cards)
        {
            var flush = IsFlush(cards);
            var straight = IsStraight(cards);
            var fourofakind = IsFourOfAKind(cards);
            var fullhouse = IsFullHouse(cards);

            if (flush.Item1 && straight.Item1)
            {
                return Tuple.Create(5, flush.Item2);
            }
            else if (fourofakind.Item1)
            {
                return Tuple.Create(4, fourofakind.Item2);
            }
            else if (fullhouse.Item1)
            {
                return Tuple.Create(3, fullhouse.Item2);
            }
            else if (flush.Item1)
            {
                //Console.WriteLine("Flush - " + flush.Item2.ToString());
                return Tuple.Create(2, flush.Item2);
            }
            else if (straight.Item1)
            {
                return Tuple.Create(1, straight.Item2);
            }
            else
            {
                return Tuple.Create<int, Card>(0, null);
            }
        }

        public static bool CheckBetterFiveCardCombo(List<Card> cards, List<Card> currentPlay)
        {
            var valueCards = EvaluateFiveCardCombo(cards);
            var valueCurrentPlay = EvaluateFiveCardCombo(currentPlay);

            if (valueCards.Item1 == 0 || valueCurrentPlay.Item1 == 0) return false;

            if (valueCurrentPlay.Item1 > valueCards.Item1) return false;
            else if (valueCards.Item1 > valueCurrentPlay.Item1) return true;
            else
            {
                return CheckBetterSingle(valueCards.Item2, valueCurrentPlay.Item2);
            }
        }

        public static bool CheckBetterCards(List<Card> cards, List<Card> currentPlay)
        {
            if (currentPlay == null)
            {
                if (cards.Count == 1)
                {
                    return true;
                }
                else if (cards.Count == 2)
                {
                    return cards[0].Rank == cards[1].Rank;
                }
                else if (cards.Count == 3)
                {
                    return cards[0].Rank == cards[1].Rank && cards[0].Rank == cards[2].Rank;
                }
                else if (cards.Count == 4)
                {
                    return false;
                }
                else if (cards.Count == 5)
                {
                    return CheckLegalFiveCardCombo(cards);
                }
                else
                {
                    return false;
                }
            }
            else if (currentPlay.Count == 1)
            {
                if (cards.Count != 1)
                {
                    return false;
                }
                else
                {
                    return CheckBetterSingle(cards[0], currentPlay[0]);
                }
            }
            else if (currentPlay.Count == 2)
            {
                if (cards.Count != 2)
                {
                    return false;
                }
                else
                {
                    return CheckBetterDouble(cards, currentPlay);
                }
            }
            else if (currentPlay.Count == 3)
            {
                if (cards.Count != 3)
                {
                    return false;
                }
                else
                {
                    return CheckBetterTriple(cards, currentPlay);
                }
            }
            else if (currentPlay.Count == 5)
            {
                if (cards.Count != 5)
                {
                    return false;
                }
                else
                {
                    return CheckBetterFiveCardCombo(cards, currentPlay);
                }
            }

            return false;
        }

        public static List<Card> GenerateDeckCards()
        {
            var cards = new List<Card>();

            var ranks = new List<char>()
            {
                'A','2','3','4','5','6','7','8','9','T','J','Q','K'
            };

            var suits = new List<char>()
            {
                'S','H','C','D'
            };

            for (var i = 0; i < ranks.Count; i++)
            {
                for (var j = 0; j < suits.Count; j++)
                {
                    cards.Add(new Card(ranks[i], suits[j]));
                }
            }

            return cards;
        }

        public static List<Card> ShuffleCards(List<Card> cards)
        {
            var result = new List<Card>();
            result.AddRange(cards);
            for (var i = 0; i < result.Count; i++)
            {
                var rnd = m_Rng.Next(0, i);
                var temp = result[i];
                result[i] = result[rnd];
                result[rnd] = temp;
            }
            return result;
        }

        public static List<int> TryToGetBestFiveCardCombo(List<Card> cards, string keyCard = "")
        {
            var result = new List<int>();
            if (cards.Count < 5) return result;

            List<Card> lastHand = null;

            for (int a = 0; a < cards.Count; a++)
            {
                for (int b = a + 1; b < cards.Count; b++)
                {
                    for (int c = b + 1; c < cards.Count; c++)
                    {
                        for (int d = c + 1; d < cards.Count; d++)
                        {
                            for (int e = d + 1; e < cards.Count; e++)
                            {
                                var currentHand = MergeSort(new List<Card>()
                                {
                                    cards[a], cards[b], cards[c], cards[d], cards[e]
                                });

                                if (!String.IsNullOrEmpty(keyCard))
                                {
                                    if (cards[a].ToString() != keyCard && cards[b].ToString() != keyCard
                                        && cards[c].ToString() != keyCard && cards[d].ToString() != keyCard
                                        && cards[e].ToString() != keyCard) continue;
                                }

                                if (CheckBetterCards(currentHand, lastHand))
                                {
                                    lastHand = currentHand;
                                    result.Clear();
                                    result.AddRange(new List<int>() { a, b, c, d, e });
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static List<int> TryToGetBestTriple(List<Card> cards, string keyCard = "")
        {
            var result = new List<int>();
            if (cards.Count < 3) return result;

            List<Card> lastHand = null;

            for (int i = cards.Count - 1; i >= 2; i--)
            {
                var currentHand = MergeSort(new List<Card>()
                {
                    cards[i], cards[i-1], cards[i-2]
                });

                if (!String.IsNullOrEmpty(keyCard))
                {
                    if (cards[i].ToString() != keyCard && cards[i-1].ToString() != keyCard
                        && cards[i-2].ToString() != keyCard) continue;
                }

                if (CheckBetterCards(currentHand, lastHand))
                {
                    lastHand = currentHand;
                    result.Clear();
                    result.AddRange(new List<int>() { i, i-1, i-2 });
                }
            }

            return result;
        }

        public static List<int> TryToGetBestDouble(List<Card> cards, string keyCard = "")
        {
            var result = new List<int>();
            if (cards.Count < 2) return result;

            List<Card> lastHand = null;

            for (int i = cards.Count - 1; i >= 1; i--)
            {
                var currentHand = MergeSort(new List<Card>()
                {
                    cards[i], cards[i-1]
                });

                if (!String.IsNullOrEmpty(keyCard))
                {
                    if (cards[i].ToString() != keyCard && cards[i - 1].ToString() != keyCard) continue;
                }

                if (CheckBetterCards(currentHand, lastHand))
                {
                    lastHand = currentHand;
                    result.Clear();
                    result.AddRange(new List<int>() { i, i - 1 });
                }
            }

            return result;
        }

        public static List<int> TryToGetBestSingle(List<Card> cards, string keyCard = "")
        {
            var result = new List<int>();
            if (cards.Count < 1) return result;

            if (!String.IsNullOrEmpty(keyCard))
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    if (card.ToString() == keyCard)
                    {
                        result.Add(i);
                        break;
                    }
                }
            }
            else result.Add(cards.Count - 1);

            return result;
        }

        public static List<int> TryToGiveOutBest(List<Card> cards, int cardCount = 0, string keyCard = "")
        {
            var result = new List<int>();
            if (cardCount == 5) result = TryToGetBestFiveCardCombo(cards, keyCard);
            else if (cardCount == 3) result = TryToGetBestTriple(cards, keyCard);
            else if (cardCount == 2) result = TryToGetBestDouble(cards, keyCard);
            else if (cardCount == 1) result = TryToGetBestSingle(cards, keyCard);
            else
            {
                result = TryToGetBestFiveCardCombo(cards, keyCard);
                if (result.Count == 0) result = TryToGetBestTriple(cards, keyCard);
                if (result.Count == 0) result = TryToGetBestDouble(cards, keyCard);
                if (result.Count == 0) result = TryToGetBestSingle(cards, keyCard);
            }
            return result;
        }

    }

    public class BigTwoRng
    {
        private RNGCryptoServiceProvider m_rngsp = new RNGCryptoServiceProvider();

        // generate a random integer (0 <= value)
        public int Next()
        {
            byte[] rb = { 0, 0, 0, 0 };
            m_rngsp.GetBytes(rb);
            int value = BitConverter.ToInt32(rb, 0);
            return value < 0 ? -value : value;
        }

        // generate a random integer, less than the maximum value (0 <= value < max)
        public int Next(int max)
        {
            if (max <= 0) return 0;
            byte[] rb = { 0, 0, 0, 0 };
            m_rngsp.GetBytes(rb);
            int value = BitConverter.ToInt32(rb, 0) % max;
            return value < 0 ? -value : value;
        }

        // generate a random integer, between the minimum value and the maximum value (min <= value < max)
        public int Next(int min, int max)
        {
            return Next(max - min) + min;
        }
    }
}
