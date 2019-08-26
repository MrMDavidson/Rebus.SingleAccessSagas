# Development Notes

## Building Releases

0. Ensure code is pushed to master
1. Update the Package Info, Assembly Version and File Version as appropriate
2. Build the code in release
```
dotnet build -c Release
```
3. Produce the nuget package
```
dotnet pack Rebus.SingleAccessSagas\Rebus.SingleAccessSagas.csproj -c Release --output ..\nupkgs
```
4. Verify the contents are correct (Unzip, double check file versions, etc)
5. Push the package to nuget
```
dotnet nuget push nupkgs\Path\To\Package -s https://nuget.org -k $NugetKey
```
6. Create a tag
```
git tag -a nuget-{nuget release version} -m "Nuget release {nuget release version}"
```
7. Push tags
```
git push --tags
```

or to only push the tag just created;
```
git push origin nuget-{nuget releaseversion}
```
