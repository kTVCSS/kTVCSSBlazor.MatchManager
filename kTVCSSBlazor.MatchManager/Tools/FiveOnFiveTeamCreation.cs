using kTVCSS.Models.Models;

namespace kTVCSSBlazor.MatchManager.Tools
{
    public class FiveOnFiveTeamCreation
    {
        public static List<User> CreateTeams(List<User> players, int playerDefaultMmr)
        {
            List<User> output = [];
            List<User> team1 = [];
            List<User> team2 = [];

            foreach (var player in players)
            {
                if (player.CurrentMMR == 0)
                {
                    player.CurrentMMR = playerDefaultMmr;
                }
            }

            var optimal = FindOptimalTeams(players);
            team1.AddRange(optimal.Item1);
            team2.AddRange(optimal.Item2);

            foreach (var player in team1)
            {
                player.TeamID = "0";
            }

            foreach (var player in team2)
            {
                player.TeamID = "1";
            }

            output.AddRange(team1);
            output.AddRange(team2);

            foreach (var player in output)
            {
                if (player.CurrentMMR == playerDefaultMmr)
                {
                    player.CurrentMMR = 0;
                }
            }

            return output;
        }

        private static (List<User>, List<User>) FindOptimalTeams(List<User> players)
        {
            int numberOfPlayers = players.Count;
            int halfTeamSize = numberOfPlayers / 2;
            double minDifference = double.MaxValue;
            List<User>? bestTeam1 = null;
            List<User>? bestTeam2 = null;

            var combinations = GetCombinations(players, halfTeamSize);

            foreach (var team1 in combinations)
            {
                var team2 = players.Except(team1).ToList();
                double avgMMRTeam1 = team1.Average(player => player.CurrentMMR);
                double avgMMRTeam2 = team2.Average(player => player.CurrentMMR);
                double difference = Math.Abs(avgMMRTeam1 - avgMMRTeam2);

                if (difference < minDifference)
                {
                    minDifference = difference;
                    bestTeam1 = team1;
                    bestTeam2 = team2;
                }
            }

            return (bestTeam1!, bestTeam2!);
        }

        private static IEnumerable<List<User>> GetCombinations(List<User> list, int length)
        {
            if (length == 1) return list.Select(x => new List<User> { x });
            return GetCombinations(list, length - 1)
                .SelectMany(t => list.Where(e => !t.Contains(e)),
                    (t1, t2) => t1.Concat(new List<User> { t2 }).ToList());
        }
    }
}
