# PetShopApp

A pet products e-commerce web application developed collaboratively for the team members CSE499 course.

The team will build the application together from scratch. This document will be updated as we set up the development environment and implement the application.

## Step 1: Install .NET SDK 10.0.402

All team members must install **.NET SDK 10.0.402** so that everyone uses the same development tools version.

### Download

[Download .NET SDK 10.0.402 Official Windows x64 Installer](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.402-windows-x64-installer)

This installer is for Windows computers with an x64 processor. If you use Windows ARM64, macOS, or Linux, let the team know so we can identify the appropriate installer for the same SDK version.

Install the **SDK**, not just the Runtime. The SDK includes the tools needed to develop, build, and run the application.

### Installation and verification

1. Download and run the installer.
2. Complete the installation.
3. Open a new terminal in VS Code or PowerShell.
4. Run:

dotnet --list-sdks

The list should include a line similar to:

10.0.402 [C:\Program Files\dotnet\sdk]

To check the SDK version selected in the current folder, run:

dotnet --version

The expected result should be: 10.0.402

If other SDK versions are installed, this last command may display a different version. Later, the project's `global.json` file will pin the SDK version to 10.0.402.

If version 10.0.402 already appears in `dotnet --list-sdks`, you do not need to reinstall it.

> IMPORTANT: Cloning the repository does not install the SDK automatically. Each team member must install it once on their computer.

## Step 2 — Clone the repository

Cloning creates a local copy of the team's repository on your computer.

### Check Git

Open a terminal in VS Code or PowerShell and run:

git --version

If the command is not recognized, install Git from [the official Git website](https://git-scm.com/downloads), then open a new terminal and try again.

### Clone and open the project folder

1. Choose a folder where you want to keep the project. Each team member may use a different local location.
2. Open a terminal in that folder.
3. Run:
   `git clone https://github.com/svpontes/petshopApp.git petshop-team cd petshop-team`  
   Git creates the `petshop-team` folder automatically. If the repository is private, sign in with your GitHub account when prompted; the repository owner must first grant you access.

4. Open VS Code and select **File > Open Folder**, then choose `petshop-team`. Alternatively, if the `code` command is available, run:

code .

### Verify the repository connection

Inside `petshop-team`, run:

git remote -v

Expected output:

origin <https://github.com/svpontes/petshopApp.git> (fetch)
origin <https://github.com/svpontes/petshopApp.git> (push)

> If you have already cloned the repository into `petshop-team`, skip the cloning command and open your existing folder. Do not clone another copy inside it.

## Install Tools

BLAZOR

### This is not necessary because blazor base is already installed

Inside petshop-team, run

dotnet new blazor --name PetShopApp --output . --framework net10.0 --interactivity Server --all-interactive --empty

C# Dev Kit, vscode extension

## Testing the application

Inside `petshop-team`, run:

dotnet run --project .\PetShopApp.csproj

type localhost:5016 in the webbrowser and press enter.
ATTENTION: if port 5016 is used for another app in your pc the output will be an error. Please use another port. Go to folder properties/lauchSettings.json and change the port. "applicationUrl": "<http://localhost:5016>",

Expected result:
A white page displaying "Hello World!"

## RUN THE APPLICATION

From vscode terminal run:
dotnet restore
dotnet run --project .\PetShoppApp.csproj~

## **\*\***\*\*\*\***\*\***VERY IMPORTANT **\*\*\*\***\*\***\*\*\*\***

## Team Development Workflow

Watch the animation to understand how we develop features locally, test them, submit pull requests, and review changes before merging.

    ## Show README in a Browser and present the animation
    # PRESS CTRL + click (Following Link)

<https://svpontes.github.io/petshopApp/>

    ## Show the ANIMATION
    # PRESS CTRL + click (Following Link)

[Watch the team workflow animation](https://svpontes.github.io/petshopApp/docs/petshop-workflow-animation.html)

## Team Favorite Quotes

Christian Peraza

> "... a mind needs books as a sword needs a whetstone, if it is to keep its edge."
> (George R. R. Martin, _A Game of Thrones_).

Rebecca's Sonderegger

> "... You'll just go home and be bored and neglected, same as always.."
> (Coraline)

Sergio Pontes

> "...Never let a problem to be solved become more important than a person to be loved."
>(Finding Joy in the Journey By President Thomas S. Monson)

Steven Kemendics

"... You call me a misanthrope, Because I avoid the company of others. You are mistaken, I love them. But in order not to hate people, I must refrain from their presents." (Casper David Friedrich)


### Restore dependencies and local tools

From the repository root, run:

```powershell
dotnet restore .\PetShopApp.csproj
dotnet tool restore
dotnet tool run dotnet-ef --version
```

The last command should display `10.0.14`. Run these commands after cloning
the repository or when package references or the tool manifest change.


### Set up the local SQLite database

After restoring the NuGet packages and local .NET tools, run these commands
from the project folder in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"

dotnet tool run dotnet-ef database update --project .\PetShopApp.csproj --context ApplicationDbContext
```

Run this command after cloning the repository and whenever you pull new
migrations. It creates or updates your local `petshop.dev.db` and adds the
initial catalog options: Dog, Cat, Food, Accessories, and Toys.

Keep the local database and its auxiliary files out of Git. Test accounts
and products created on one developer's machine are not shared with the team.

To test product management, sign in with a local account that has the `Admin`
role and select **Manage Products** from the admin dashboard. New accounts
created through the registration page have the `Customer` role by default.