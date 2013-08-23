
/*
 
 Ranking System, by Wolfje (Tyler W.), 2013.
 
 This is a ranking script for the AliasCmd plugin in Javascript.  Install it by putting it in your Tshock\SEconomy" directory and starting the Terraria server.
 If the server is already running AliasCmd just type "/aliascmd reload" in the console to cause the script to load.
 
 Released under Wolfje's Don't-be-a-dick license.  You didn't write this, I did, so if you modify it don't claim the work as yours; it isn't.
 
 
 
 
 --- rankingList Object ---
 
 There's really nothing to it. :)
 
 * the first parameter is the rank key.
 * "name" must match the rank key.  Avoid spaces
 * "cost" is a SEconomy string representation of how much money it costs to be that rank, "0c" is free
 * "group" is the TShock group this rank is
 
 * "parentgroup" is used for hierarchy.  set it to the group of the rank that comes before it to establish a ladder
 You can specify multiple ranks with the same parent.  Doing this is going to create a tree of choices called a class trunk.
 Once a user arrives at a class trunk they cannot /rank up, they will be asked to pick a "class", but you can modify that text to whatever you like.
 
 You can specify as many trunks as you want.  You can have rank choices inside rank choices up to an infinite depth, the sky (or rather, your heap size) is the limit. :)
 
 */
var rankingList = {
    "level1": {
        "name": "level1",
        "parentgroup": undefined,
        "group": "level1",
        "cost": "0c"
    },
    "level2": {
        "name": "level2",
        "parentgroup": "level1",
        "levelupcommands": [
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/i goldfish",
            "/time noon",
            "/spawnmob \"skeletron head\"",
        ],
        "group": "level2",
        "cost": "5g"
    },
    "level3": {
        "name": "level3",
        "parentgroup": "level2",
        "group": "level3",
        "cost": "10g"
    },
    "level4": {
        "name": "level4",
        "parentgroup": "level3",
        "group": "level4",
        "cost": "20g"
    },
    "level5": {
        "name": "level5",
        "parentgroup": "level4",
        "group": "level5",
        "cost": "40g"
    },
    //This is where the tree splits,
    "split1": {
        "name": "split1",
        "parentgroup": "level5",
        "group": "split1",
        "cost": "80g"
    },
    "split1_2": {
        "name": "split1_2",
        "parentgroup": "split1",
        "group": "split1_2",
        "cost": "80g"
    },
    "split1_3": {
        "name": "split1_3",
        "parentgroup": "split1_2",
        "group": "split1_3",
        "cost": "80g"
    },
    //this is the second split, notice that the parent of both the splits are level5
    "split2": {
        "name": "split2",
        "parentgroup": "level5",
        "group": "split2",
        "cost": "80g"
    },
    "split2_2": {
        "name": "split2_2",
        "parentgroup": "split2",
        "group": "split2_2",
        "cost": "80g"
    },
    "split2_3": {
        "name": "split2_3",
        "parentgroup": "split2_2",
        "group": "split2_3",
        "cost": "80g"
    }
}

/*
 ------------------------------------------------------------------------------------------------------------------------------------------------------
 */


function find_parent(parentgroup) {
    var parentList = new Array();

    for (var parentgroupRank in rankingList) {
        if (rankingList.hasOwnProperty(parentgroupRank)) {
            //assign next rank if match
            if (rankingList[parentgroupRank].parentgroup == parentgroup) {
                parentList.push(rankingList[parentgroupRank]);
            }
        }
    }

    return parentList;
}

function find_rank(group) {
    for (var rank in rankingList) {
        if (rankingList.hasOwnProperty(rank)) {
            if (rankingList[rank].group == group) {
                return rankingList[rank];
            }
        }
    }
}

function find_rank_by_name(name) {
    for (var rank in rankingList) {

        if (rankingList.hasOwnProperty(rankingList[rank].name)) {
            broadcast(rankingList[rank].name.toLowerCase() + " " + name.toLowerCase());

            if (rankingList[rank].name.toLowerCase() == name.toLowerCase()) {
                return rankingList[rank];
            }
        }
    }
}

function starting_rank() {
    for (var rank in rankingList) {
        if (rankingList.hasOwnProperty(rank)) {
            if (rankingList[rank].parentgroup === undefined) {
                return rankingList[rank];
            }
        }
    }
}

/**
 Assigns the player to the rank, charges them if needed
 */
function move_rank(player, rank) {
    if (player && rank) {
        var rankName = rank.name;
        var rankCost = seconomy_parse_money(rank.cost);

        if (rankCost != "" && rankCost != "0c") {

            try {
                var account = seconomy_get_account(player);

                if (account) {
                    seconomy_pay_async(account, seconomy_world_account(), rankCost, "JSRank: rank " + rankName, function (transferArgs) {
                        if (transferArgs.TransferSucceeded == true) {
                            change_group(player, rank.group);

                            for (i in rank.levelupcommands) {
                                execute_command(player, rank.levelupcommands[i]);
                            }

                            broadcast(player.Name + " has become a " + rankName + "!");
                        } else {
                            msg(player, "Could not rank you up because your payment failed.");
                        }
                    });
                } else {
                    msg(player, "* To rank up, you need a bank account.  Please sign in.");
                }
            } catch (ex) {
                msg(player, "charge error: ", ex);
            }

        } else {
            //rank is free

            change_group(player, rank.group);

            for (i in rank.levelupcommands) {
                execute_command(player, rank.levelupcommands[i]);
            }

            broadcast(player.Name + " has become a " + rankName + "!");
        }
    }
}

/**
 Figures out what the player's next rank is or if they are at the trunk of a class tree
 */
function move_next_rank(player) {
    var rank = find_rank(player.Group.Name);

    if (rank) {
        var nextRankList = find_parent(rank.group);

        if (nextRankList.length == 0) {
            msg(player, "You are already the maximum rank!");
        } else if (nextRankList.length == 1) {
            move_rank(player, nextRankList[0]);
        } else if (nextRankList.length > 1) {
            msg(player, "You are a " + rank.name + ". Now you must pick a class:");

            for (i = 0; i < nextRankList.length; i++) {
                var rankObject = nextRankList[i];

                if (seconomy_parse_money(rankObject.cost).Value > 0) {
                    msg(player, " * /rank " + rankObject.name + " (costs " + rankObject.cost + ")");
                } else {
                    msg(player, " * /rank " + rankObject.name + " (free)");
                }
            }
        }
    } else {
        move_rank(player, starting_rank());
    }
}

function print_player_help(player) {
    var rank = find_rank(player.Group.Name);
    var nextRank;
    var playerText;

    if (rank.name) {
        playerText = "You are a " + rank.name + ".";
        nextRank = find_parent(rank.group);


        if (nextRank.length == 1) {
            playerText += " Your next rank is " + nextRank[0].name;

            if (nextRank[0].cost != "" && nextRank[0].cost != "0c") {
                playerText += " and costs " + nextRank[0].cost;
            } else {
                playerText += ".";
            }

        } else if (nextRank.length > 1) {
            //player is at a trunk and must pick a class
            playerText += " Now you must pick a class:";
            msg(player, playerText);

            for (i = 0; i < nextRank.length; i++) {
                var _r = nextRank[i];
                var _rText = _r.name;

                //append cost info if there is any
                if (seconomy_parse_money(_r.cost).Value > 0) {
                    _rText += " (costs " + _r.cost + ")";
                } else {
                    _rText += " (free)";
                }

                msg(player, " * /rank " + _rText);
            }

            return;
        } else {
            //there is nowhere to go.
            playerText += " You are the maximum rank!";
        }

        msg(player, playerText);
    } else {
        msg(player, "You aren't a rank yet.  Your next rank is " + starting_rank().name);
    }
}

/**
 /rank alias command
 
 The command itself costs nothing, that's because the handlers will charge if need be.
 */
create_alias("rank", "0c", 0, "", function (player, parameters) {
    var rank = find_rank(player.Group.Name);
    var nextRank;

    if (rank) {
        nextRank = find_parent(rank.group);
    }

    if (parameters.Count == 0) {
        print_player_help(player);
    } else {
        if (parameters[0] == "up") {
            move_next_rank(player);
        } else if (parameters[0] == "help") {

            print_player_help(player);

        } else if (parameters[0] != undefined) {
            var chosenClass = find_rank_by_name(parameters[0].toString().toLowerCase());
            var group = tshock_group(chosenClass.group);

            if (group) {
                move_rank(player, chosenClass);
            }
        }
    }
});