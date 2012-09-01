require "albacore"
require_relative "filesystem"
require_relative "gallio-task"

reportsPath = "reports"
version = ENV["BUILD_NUMBER"]

task :build => :createPackage
task :deploy => :pushPackage

assemblyinfo :assemblyInfo do |asm|
    asm.version = version
    asm.company_name = "Ultraviolet Catastrophe"
    asm.product_name = "Kato"
    asm.title = "Kato"
    asm.description = "SMTP Server Library"
    asm.copyright = "Copyright (c) 2012 Ultraviolet Catastrophe"
    asm.output_file = "src/Kato/Properties/AssemblyInfo.cs"
end

msbuild :buildLibrary => :assemblyInfo do |msb|
    msb.properties :configuration => :Release
    msb.targets :Clean, :Build
    msb.solution = "src/Kato/Kato.csproj"
end

msbuild :buildTests => :buildLibrary do |msb|
    msb.properties :configuration => :Release
    msb.targets :Clean, :Build
    msb.solution = "src/Tests/Tests.csproj"
end

task :unitTestInit do
	FileSystem.EnsurePath(reportsPath)
end

gallio :unitTests => [:buildTests, :unitTestInit] do |runner|
	runner.echo_command_line = true
	runner.add_test_assembly("src/Tests/bin/Release/Tests.dll")
	runner.verbosity = 'Normal'
	runner.report_directory = reportsPath
	runner.report_name_format = 'tests'
	runner.add_report_type('Html')
end

nugetApiKey = ENV["NUGET_API_KEY"]
deployPath = "deploy"

packagePath = File.join(deployPath, "package")
nuspecFilename = "kato.nuspec"
packageLibPath = File.join(packagePath, "lib")
binPath = "src/Kato/bin/release"

task :prepPackage => :unitTests do
	FileSystem.DeleteDirectory(deployPath)
	FileSystem.EnsurePath(packageLibPath)
	FileSystem.CopyFiles(File.join(binPath, "Kato.dll"), packageLibPath)
	FileSystem.CopyFiles(File.join(binPath, "Kato.pdb"), packageLibPath)
end

nuspec :createSpec => :prepPackage do |nuspec|
   nuspec.id = "kato"
   nuspec.version = version
   nuspec.authors = "Mike O'Brien"
   nuspec.owners = "Mike O'Brien"
   nuspec.title = "Kato"
   nuspec.description = "SMTP Server Library"
   nuspec.summary = "SMTP Server Library"
   nuspec.language = "en-US"
   nuspec.licenseUrl = "https://github.com/mikeobrien/kato/blob/master/LICENSE"
   nuspec.projectUrl = "https://github.com/mikeobrien/kato"
   nuspec.iconUrl = "https://github.com/mikeobrien/kato/raw/master/misc/logo.png"
   nuspec.working_directory = packagePath
   nuspec.output_file = nuspecFilename
   nuspec.tags = "smtp"
end

nugetpack :createPackage => :createSpec do |nugetpack|
   nugetpack.nuspec = File.join(packagePath, nuspecFilename)
   nugetpack.base_folder = packagePath
   nugetpack.output = deployPath
end

nugetpush :pushPackage => :createPackage do |nuget|
    nuget.apikey = nugetApiKey
    nuget.package = File.join(deployPath, "kato.#{version}.nupkg").gsub('/', '\\')
end