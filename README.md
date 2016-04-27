# Commons.Refactorings
A Roslyn-based refactorings plugin for Visual Studio

VERY VERY BETA!!!

Currently only two refactorings:
1- "Break into partials" refactoring for classes with more that a certain number of members. Breaks into many partial classes inside the same source file.
2- "Move partial to new source file" refactoring that moves a partial class to another source, whose name is the current one appended with the first method's identifier or 'Partial' if no method is found inside the partial class.
