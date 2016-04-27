# Commons.Refactorings
A Roslyn-based refactorings plugin for Visual Studio

VERY VERY BETA!!!

Currently only two refactorings:

 1. __"Break into partials"__ refactoring for classes with more that a certain number of members. Breaks into many partial classes inside the same source file.
 
 2. __"Move partial to new source file"__ refactoring that moves a partial class to another source, whose name is the current one appended with the first method's identifier or 'Partial' if no method is found inside the partial class.
