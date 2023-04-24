using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Parse;
using Parse.Infrastructure;

public class ParseRockPaperScissorsGame : MonoBehaviour
{
	public string applicationId;
	public string serverURI;
	public string masterKey;
	public string playerName;
	public GameObject playBoard;
	public GameObject paper;
	public GameObject scissors;
	public GameObject rock;
	public GameObject paperOpponent;
	public GameObject scissorsOpponent;
	public GameObject rockOpponent;
	public GameObject paperWinAudio;
	public GameObject scissorsWinAudio;
	public GameObject rockWinAudio;
    public TMP_Dropdown dropdown;
    public TMP_Text outcomeTextMesh;

    private ParseClient client;
    private System.Random rnd = new System.Random();
    private float timeSinceLastUpdate;
    private string opponent;
    private bool isWaitingOnReply = false;
    private bool isCheckingOnReply = false;
    private string playerThrow;
    private string computerThrowString;
    private string COMPUTER = "computer";
    private string ROCKPAPERSCISSORSPLAYERS_CLASS = "RockPaperScissorsPlayers"; 
    private string ROCKPAPERSCISSORS_CLASS = "RockPaperScissors"; 
    private string ROCK = "rock", PAPER = "paper", SCISSORS = "scissors"; 
    private string PLAYERNAME = "playerName", OPPONENTNAME = "opponentName", PLAYERTHROW = "playerThrow";


    //create the ParseClient connection and setup the playing board with dropdown list of players
    async void Start()
    {
	    CreateParseClient();
        dropdown.onValueChanged.AddListener(OnDropdownValueChange);
		try 
		{
			await client.LogInAsync(username: "test", password: "test"); 
		}
		catch (Exception e) // could be more specific re Exception
		{
            Debug.Log("LogInAsync failed. This is expected if this is the first use. Exception:" + e);
            Debug.Log("Creating user...");
  			await client.SignUpAsync(username:  "test", password:  "test");
		}
    	ParseQuery<ParseObject> query = new ParseQuery<ParseObject>(client, ROCKPAPERSCISSORSPLAYERS_CLASS);
    	Debug.Log("done ParseQuery query:" +  query);
    	var results = await query.FindAsync();
    	Debug.Log("done results:" +  results);
		List<string> options = new List<string>();
		options.Add("Choose opponent...");
    	foreach (ParseObject obj in results)
    	{
       		string objectId = obj.ObjectId;
       		string playerName = obj.Get<string>(PLAYERNAME);
            options.Add(playerName);
       		Debug.Log("done playerName:" +  playerName);
    	}
        if (options.Contains(playerName))
        {
	        options.Remove(playerName); // do not add self to list
        } else
        {
	        ParseObject psrGame = new ParseObject(ROCKPAPERSCISSORSPLAYERS_CLASS);
	        psrGame[PLAYERNAME] = playerName;
	        await psrGame.SaveAsync();
        }

        if (!options.Contains(COMPUTER))
        {
	        ParseObject psrGame = new ParseObject(ROCKPAPERSCISSORSPLAYERS_CLASS);
	        psrGame[PLAYERNAME] = COMPUTER;
	        await psrGame.SaveAsync();
	        options.Add(COMPUTER);
        }
    	dropdown.ClearOptions(); 
    	dropdown.AddOptions(options.Distinct().ToList());
    }
    void CreateParseClient()
    {
	    client = new ParseClient(
		    new ServerConnectionData
		    {
			    ApplicationID = applicationId,
			    ServerURI = serverURI,
			    Key = "",
			    MasterKey = masterKey
		    },
		    new LateInitializedMutableServiceHub { },
		    new MetadataMutator
		    {
			    EnvironmentData = new EnvironmentData
			    {
				    OSVersion = SystemInfo.operatingSystem,
				    Platform = $"Unity {Application.unityVersion} on {SystemInfo.operatingSystemFamily}",
				    TimeZone = TimeZoneInfo.Local.StandardName
			    },
			    HostManifestData = new HostManifestData
			    {
				    Name = Application.productName, Identifier = Application.productName,
				    ShortVersion = Application.version, Version = Application.version
			    }
		    },
		    new AbsoluteCacheLocationMutator
		    {
			    CustomAbsoluteCacheFilePath =
				    $"{Application.persistentDataPath.Replace('/', Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}Parse.cache"
		    }
	    );
	    client.Publicize();
	    Debug.Log("client:" + client);
    }
    void OnDropdownValueChange(int value)
    {
	    Reset();
		if(value == 0) return; // ie if it equals "Choose your opponent..."
		opponent = dropdown.options[value].text;
        Debug.Log("Selected opponent: " + opponent );
		playBoard.SetActive(true);
    }

    public void Rock()
    {
	    Debug.Log("player throws rock");
	    rock.SetActive(true);
	    insertPlayerThrow(ROCK);
    }
    
    public void Paper()
    {
	    Debug.Log("player throws paper");
	    paper.SetActive(true);
	    insertPlayerThrow(PAPER);
    }
    
    public void Scissors()
    {
	    Debug.Log("player throws scissors");
	    scissors.SetActive(true);
	    insertPlayerThrow(SCISSORS);
    }

    async void insertPlayerThrow(string theThrow)
    {
	    playerThrow = theThrow;
	    ParseObject psrGame = new ParseObject(ROCKPAPERSCISSORS_CLASS);
	    psrGame[PLAYERNAME] = playerName;
	    psrGame[OPPONENTNAME] = opponent;
	    psrGame[PLAYERTHROW] = playerThrow;
	    Debug.Log("about to SaveAsync player:" + psrGame);
	    await psrGame.SaveAsync();
	    if (string.Equals(opponent, "computer"))
	    {
		    psrGame = new ParseObject(ROCKPAPERSCISSORS_CLASS);
		    psrGame[PLAYERNAME] = "computer";
		    psrGame[OPPONENTNAME] = playerName;
		    psrGame[PLAYERTHROW] = getComputersThrow();
		    Debug.Log("about to SaveAsync computer:" + psrGame);
		    await psrGame.SaveAsync();
	    }
	    isWaitingOnReply = true;
    }

    //check the opponents throw/move and react accordingly as far as image and sound for win
    //cleanup/delete the opponents move from the database
    //do not cleanup/delete the players move as the opponent may have not have scene it.
    //ie players are responsible for cleanup/delete of opponents moves made against them
    async void checkOpponent()
    {
	    ParseQuery<ParseObject> query = new ParseQuery<ParseObject>(client, ROCKPAPERSCISSORS_CLASS);
	    query = query.WhereEqualTo(PLAYERNAME, opponent).WhereEqualTo(OPPONENTNAME, playerName);
	    var results = await query.FindAsync(); 
	    foreach (ParseObject obj in results)
	    {
		    string opponentsMove = obj.Get<string>(PLAYERTHROW);
		    Debug.Log("opponentsMove:" + opponentsMove);
		    if (string.Equals(opponentsMove, "")) isWaitingOnReply = true;
		    else
		    {
			    switch (opponentsMove)
			    {
				    case "rock":
					    rockOpponent.SetActive(true);
					    if (string.Equals(opponentsMove, playerThrow)) outcomeTextMesh.text = "Tie";
					    else if (string.Equals(playerThrow, PAPER))
					    {
						    outcomeTextMesh.text = "You won!";
						    paperWinAudio.SetActive(true);
					    }
					    else // SCISSORS
					    {
						    outcomeTextMesh.text = opponent + " won.";
						    rockWinAudio.SetActive(true);
					    }
					    break;
				    case "paper":
					    paperOpponent.SetActive(true);
					    if (string.Equals(opponentsMove, playerThrow)) outcomeTextMesh.text = "Tie";
					    else if (string.Equals(playerThrow, SCISSORS))
					    {
						    outcomeTextMesh.text = "You won!";
						    scissorsWinAudio.SetActive(true);
					    }
					    else // ROCK
					    {
						    outcomeTextMesh.text = opponent + " won.";
						    paperWinAudio.SetActive(true);
					    }
					    break;
				    case "scissors":
					    scissorsOpponent.SetActive(true);
					    if (string.Equals(opponentsMove, playerThrow)) outcomeTextMesh.text = "Tie";
					    else if (string.Equals(playerThrow, ROCK))
					    {
						    outcomeTextMesh.text = "You won!";
						    rockWinAudio.SetActive(true);
					    }
					    else // PAPER
					    {
						    outcomeTextMesh.text = opponent + " won.";
						    scissorsWinAudio.SetActive(true);
					    }
					    break;
				    default:
					    Debug.Log("unknown move:" + opponentsMove);
					    break;
			    }
			    await obj.DeleteAsync(); 
			    isWaitingOnReply = false;
			    if (!string.Equals(opponent, COMPUTER)) return;
			    query = new ParseQuery<ParseObject>(client, ROCKPAPERSCISSORS_CLASS);
			    query = query.WhereEqualTo(PLAYERNAME, playerName).WhereEqualTo(OPPONENTNAME, COMPUTER);
			    results = await query.FindAsync(); 
			    foreach (ParseObject compobj in results) await compobj.DeleteAsync();
		    }
	    }
    }
    
    // if the opponent is computer then it already knows the outcome :) and so can delete the player's record
    async void deleteIfComputer()
    {
	    if (string.Equals(opponent, COMPUTER)) return;
	    ParseQuery<ParseObject> query = new ParseQuery<ParseObject>(client, ROCKPAPERSCISSORS_CLASS);
	    query = query.WhereEqualTo(PLAYERNAME, playerName).WhereEqualTo(OPPONENTNAME, COMPUTER);
	    var results = await query.FindAsync(); 
	    foreach (ParseObject obj in results) await obj.DeleteAsync();
    }

    private string getComputersThrow()
    {
        switch (rnd.Next(2) + 1)
        {
            case 1:
	            return PAPER;
            case 2:
	            return SCISSORS;
            case 3:
	            return ROCK;
            default:
                return "unknown";
        }
    }

    private void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        if (isWaitingOnReply && !isCheckingOnReply && timeSinceLastUpdate >= 3f)
        {
			isCheckingOnReply = true;
            Debug.Log("Checking for opponents move...");
			checkOpponent();
            timeSinceLastUpdate = 0f;
            isCheckingOnReply = false;
        }
    }

	public void Reset() {
		Debug.Log("Reset...");
		paper.SetActive(false);
		scissors.SetActive(false);
		rock.SetActive(false);
		paperOpponent.SetActive(false);
		scissorsOpponent.SetActive(false);
		rockOpponent.SetActive(false);
		paperWinAudio.SetActive(false);
		scissorsWinAudio.SetActive(false);
		rockWinAudio.SetActive(false);
		outcomeTextMesh.text = "";
		Debug.Log("Reset complete");
	}

	async public void DeleteMyGames()
	{
		Debug.Log("DeleteMyGames...");
		ParseQuery<ParseObject> query = new ParseQuery<ParseObject>(client, ROCKPAPERSCISSORS_CLASS);
		query = query.WhereEqualTo(PLAYERNAME, playerName);
		var results = await query.FindAsync();
		foreach (ParseObject obj in results) await obj.DeleteAsync();
		Debug.Log("DeleteMyGames complete");
	}
}

