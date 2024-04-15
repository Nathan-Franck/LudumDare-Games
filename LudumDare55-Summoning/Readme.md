# Ludum Dare 55 - Summoning

Game is Published!
* [Ludum Dare Game Jam](https://ldjam.com/events/ludum-dare/55/soul-technician)
* [Itch.io](https://nathan-franck.itch.io/ludum-dare-55)

![Gameplay Screenshot](./gameplay_screenshot.png)

# Development Environment

* Install VSCode - https://code.visualstudio.com/download
    * Get the Zig extension for VSCode
* Install Bun - https://bun.sh/
* Update your Environment PATH
    * Find the path to Zig from your VSCode User Settings JSON file and add the Zig's parent folder to the PATH
    * Bun path should have been updated automatically, can double check


# Running the Game

From the command line:
```
zig build wasm
zig run ./src/tool_game_build_type_definitions.zig
bun run dev
```
> You should be able to ctrl+click the link to open up the URL to the game!

![alt text](image.png)

# Architecture

* Zig in the backend running the game state machine, passes concrete data about the current state of the game to the front-end to display.
  * [./src/game.zig](./src/game.zig) Contains all game logic, as well as packing in all of the game's assets (images) into the wasm bundle
  * [./src/type_definitions.zig](./src/type_definitions.zig) Generates a Typescript d.ts file that communicates the exact JSON structure being sent from the back-end to the front-end
  * [./src/wasm_entry.zig](./src/wasm_entry.zig) Recieves and provides JSON from the front-end, while sending slice data for really large data chunks (image data)
* React frontend reads in state data and displays to user, passes back input events from the user to the backend game state machine to update.
  * [./web-src/shaderBuilder.ts](./web-src/shaderBuilder.ts) Wraps WebGL complexity in a typesafe wrapper, making refactoring names and providing proper data types easy
  * [./web-src/app.tsx](./web-src/app.tsx) Contains the Game's graphics as well as a full implementation of the EaselJS spritesheet animation JSON standard for the game's sprites
 
Making games web-first is great - since for small games, a small amount of people can play a lot of web-games without worring about compatability, viruses, security. Web also allows your game to be played on a variet of devices. Having the core of the game run in Zig means that later, it could be possible to port the game to non-web platforms and only worry about interfacing with input, rendering and sound for that platform.

# Game Design
* 4 Machines are working on a summoning
* Machines break down and you have to fix them
* If too many machines break down then the stage resets (4 stages of increasing difficulty)

# TODO

### Nathan
#### Saturday
* Actually implement EaselJS format
* Timed animations displaying
* Character controller - up down left right animations and x,y movement - work for controller and keyboard
#### Sunday
* Machines break down
* Character interaction with machines, A button, space bar,  play the interaction animation, machine is fixed (new graphic)
* Summoning animation (win) -> next stage reset
* Losing animation - text says "TOO BROKEN" -> current stage reset
* Win -> pause -> text says "SUMMONING COMPLETE"
#### Stretch Goals
* Obstacles - goo, electricity, ??? Something to slow you down while trying to get to the broken machine
* Main Menu, menu transitions

### Oscar
* Background (1080p 16:9)
* Summoning Chamber progess animation
* Summoning Chamber win animation
* Summoning Chamber fail animation
* Ghost (4 directions, idle animations, fixing animation (just one direction, we'll flip in runtime))
* Victory body animation
* Machine (4x) working idle animation
* Machine broken (animation?)
#### Stretch Goals
* Machine breakdown effect
* Fix effect
* Source Music and Audio
