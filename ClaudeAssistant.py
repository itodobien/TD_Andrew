# Filename: pt.py

import os
import argparse
from datetime import datetime
import json
import yaml
import subprocess

def generate_tree(root_dir: str, exclude: set) -> str:
    output = []
    root_dir = os.path.abspath(root_dir)

    def should_include(name):
        return not any(name.startswith(ex) for ex in exclude)

    def add_directory(path, prefix=""):
        dir_name = os.path.basename(path)
        if not should_include(dir_name):
            return

        output.append(f"{prefix}{dir_name}/")
        
        try:
            items = sorted(os.listdir(path))
        except PermissionError:
            output.append(f"{prefix}??? <Permission Denied>")
            return

        dirs = [item for item in items if os.path.isdir(os.path.join(path, item)) and should_include(item)]
        files = [item for item in items if os.path.isfile(os.path.join(path, item)) and should_include(item)]

        for i, dir_name in enumerate(dirs):
            is_last = (i == len(dirs) - 1 and len(files) == 0)
            new_prefix = prefix + ("??? " if is_last else "??? ")
            next_prefix = prefix + ("    " if is_last else "?   ")
            add_directory(os.path.join(path, dir_name), new_prefix)
        
        for i, file_name in enumerate(files):
            is_last = (i == len(files) - 1)
            output.append(f"{prefix}{'??? ' if is_last else '??? '}{file_name}")

    add_directory(root_dir)
    return "\n".join(output)

def read_asset_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as file:
        content = file.read()
        # Try parsing as YAML first
        try:
            data = yaml.safe_load(content)
            return json.dumps(data, indent=2)[:1000]  # Return first 1000 characters as JSON
        except yaml.YAMLError:
            # If YAML parsing fails, try JSON
            try:
                data = json.loads(content)
                return json.dumps(data, indent=2)[:1000]  # Return first 1000 characters
            except json.JSONDecodeError:
                # If both fail, return a portion of the raw content
                return content[:1000]  # Return first 1000 characters of raw content

def get_build_settings(project_path):
    build_settings_path = os.path.join(project_path, 'ProjectSettings', 'EditorBuildSettings.asset')
    if os.path.exists(build_settings_path):
        return read_asset_file(build_settings_path)
    return "Build settings file not found"

def get_package_versions(project_path):
    packages_lock_path = os.path.join(project_path, 'Packages', 'packages-lock.json')
    if os.path.exists(packages_lock_path):
        with open(packages_lock_path, 'r') as file:
            data = json.load(file)
            return json.dumps(data, indent=2)
    return "Package lock file not found"

def get_project_settings(project_path):
    project_settings_path = os.path.join(project_path, 'ProjectSettings', 'ProjectSettings.asset')
    if os.path.exists(project_settings_path):
        return read_asset_file(project_settings_path)
    return "Project settings file not found"

def get_quality_settings(project_path):
    quality_settings_path = os.path.join(project_path, 'ProjectSettings', 'QualitySettings.asset')
    if os.path.exists(quality_settings_path):
        return read_asset_file(quality_settings_path)
    return "Quality settings file not found"

def get_scene_hierarchies(project_path):
    scenes_path = os.path.join(project_path, 'Assets', 'Scenes')
    hierarchies = {}
    if os.path.exists(scenes_path):
        for scene_file in os.listdir(scenes_path):
            if scene_file.endswith('.unity'):
                scene_path = os.path.join(scenes_path, scene_file)
                hierarchies[scene_file] = read_asset_file(scene_path)
    return hierarchies

def get_unity_version(project_path):
    version_file = os.path.join(project_path, 'ProjectSettings', 'ProjectVersion.txt')
    if os.path.exists(version_file):
        with open(version_file, 'r') as file:
            return file.read().strip()
    return "Unity version information not found"

def get_git_info(project_path):
    try:
        branch = subprocess.check_output(['git', 'rev-parse', '--abbrev-ref', 'HEAD'], cwd=project_path).decode().strip()
        commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=project_path).decode().strip()
        return f"Branch: {branch}\nLast commit: {commit}"
    except subprocess.CalledProcessError:
        return "Git information not available"

def count_assets(project_path):
    asset_counts = {
        'Scenes': 0,
        'Prefabs': 0,
        'Materials': 0,
        'Textures': 0,
        'Scripts': 0
    }

    for root, dirs, files in os.walk(os.path.join(project_path, 'Assets')):
        for file in files:
            if file.endswith('.unity'):
                asset_counts['Scenes'] += 1
            elif file.endswith('.prefab'):
                asset_counts['Prefabs'] += 1
            elif file.endswith('.mat'):
                asset_counts['Materials'] += 1
            elif file.endswith(('.png', '.jpg', '.jpeg', '.tga')):
                asset_counts['Textures'] += 1
            elif file.endswith('.cs'):
                asset_counts['Scripts'] += 1

    return asset_counts

def main():
    parser = argparse.ArgumentParser(description="Generate an enhanced tree-like structure of the Unity project directory.")
    parser.add_argument("--root-dir", default=".", help="The root directory to start from (default: current directory)")
    
    args = parser.parse_args()
    
    if args.root_dir == ".":
        args.root_dir = os.path.dirname(os.path.abspath(__file__))

    exclude = {
        ".", "..", ".git", ".vs", ".vscode", "__pycache__", "node_modules",
        "bin", "obj", "build", "dist", "target",
        "Temp", "Library", "Logs", "UserSettings",  # Unity-specific
    }
    
    tree = generate_tree(args.root_dir, exclude)
    build_settings = get_build_settings(args.root_dir)
    package_versions = get_package_versions(args.root_dir)
    project_settings = get_project_settings(args.root_dir)
    quality_settings = get_quality_settings(args.root_dir)
    scene_hierarchies = get_scene_hierarchies(args.root_dir)
    unity_version = get_unity_version(args.root_dir)
    git_info = get_git_info(args.root_dir)
    asset_counts = count_assets(args.root_dir)
    
    output = f"""
Project Tree:
{tree}

Build Settings:
{build_settings}

Package Versions:
{package_versions}

Project Settings:
{project_settings}

Quality Settings:
{quality_settings}

Scene Hierarchies:
{json.dumps(scene_hierarchies, indent=2)}

Unity Version:
{unity_version}

Git Information:
{git_info}

Asset Counts:
{json.dumps(asset_counts, indent=2)}
    """
    
    # Generate a filename with a timestamp
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = f"project_structure_{timestamp}.txt"
    
    # Save the output to a file in the current working directory
    with open(filename, 'w', encoding='utf-8') as f:
        f.write(output)
    
    print(output)
    print(f"\nEnhanced project structure and additional info saved to {filename}")

if __name__ == "__main__":
    main()
