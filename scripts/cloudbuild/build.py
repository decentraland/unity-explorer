import os
import stat
import re
import sys
import time
import shutil
import zipfile
import requests
import datetime
import argparse
# Local
import utils

from zipfile import ZipFile, ZipInfo

class ZipFileWithPermissions(zipfile.ZipFile):
    def _extract_member(self, member, targetpath, pwd):
        if not isinstance(member, zipfile.ZipInfo):
            member = self.getinfo(member)

        targetpath = super()._extract_member(member, targetpath, pwd)

        attr = member.external_attr >> 16
        if attr != 0:
            os.chmod(targetpath, attr)
        return targetpath


# Define whether this is a release workflow based on IS_RELEASE_BUILD
is_release_workflow = os.getenv('IS_RELEASE_BUILD', 'false').lower() == 'true'

URL = utils.create_base_url(os.getenv('ORG_ID'), os.getenv('PROJECT_ID'))
HEADERS = utils.create_headers(os.getenv('API_KEY'))
POLL_TIME = int(os.getenv('POLL_TIME', '60')) # Seconds

build_healthy = True

parser = argparse.ArgumentParser()
parser.add_argument('--resume', help='Resume tracking a running build stored in build_info.json', action='store_true')
parser.add_argument('--cancel', help='Cancel a running build stored in build_info.json', action='store_true')
parser.add_argument('--delete', help='Delete build target after PR is closed or merged', action='store_true')

def validate_branch_name(branch_name):
    #Validates the branch name to ensure it does not contain special characters like +, ., or @."""
    if re.search(r'[+\.@]', branch_name):
        print(f"Error: Branch name '{branch_name}' contains invalid characters (+, ., or @).")
        sys.exit(1)

def get_target(target):
    response = requests.get(f'{URL}/buildtargets/{target}', headers=HEADERS)

    print(f'get_target request url: "{URL}/buildtargets/{target}"')

    if response.status_code == 200:
        return response.json()
    elif response.status_code == 404:
        print(f'Target "{target}" does not exist (yet?)')
        return response.json()
    else:
        print("Failed to get target data with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def clone_current_target(use_cache):
    def generate_body(template_target, name, branch, options, remoteCacheStrategy):
        body = get_target(template_target)

        body['name'] = name
        body['settings']['scm']['branch'] = branch
        body['settings']['advanced']['unity']['playerExporter']['buildOptions'] = options
        body['settings']['remoteCacheStrategy'] = remoteCacheStrategy
        body['settings']['buildSchedule']['isEnabled'] = False

        print(f"Using cache strategy target: {remoteCacheStrategy}")

        # Remove cache for new targets
        if 'buildTargetCopyCache' in body['settings']:
            del body['settings']['buildTargetCopyCache']

        return body

    # Set target name based on branch, without commit SHA
    base_target_name  = f'{re.sub(r'^t_', '', os.getenv('TARGET'))}-{re.sub('[^A-Za-z0-9]+', '-', os.getenv('BRANCH_NAME'))}'.lower()

    print(f"Start clone_current_target for {base_target_name}")
    if is_release_workflow:
         # Use the tag version in the target name if it's a release workflow
        tag_version = os.getenv('TAG_VERSION', 'unknown-version')
        # Remove dots from the tag version, as unity API does not allow . in the request
        sanitized_tag_version = re.sub(r'\.', '-', tag_version)
        new_target_name = f"{base_target_name}-{sanitized_tag_version}"
    else:
        new_target_name = base_target_name

    template_target = os.getenv('TARGET')

    # Generate request body
    body = generate_body(
        template_target,
        new_target_name,
        os.getenv('BRANCH_NAME'),
        os.getenv('BUILD_OPTIONS').split(','),
        os.getenv('CACHE_STRATEGY'))

    existing_target = get_target(new_target_name)
    
    if 'error' in existing_target:
        print(f"New target found")
        # Create new target with template cache
        if use_cache:
            body['settings']['buildTargetCopyCache'] = template_target
            print(f"Using template cache build target: {template_target}")
        else:
            print(f"Not using cache")
        try:
            response = requests.post(f'{URL}/buildtargets', headers=HEADERS, json=body)
        except ConnectionError as e:
            print(f'ConnectionError while trying to post new target: {e}. Retrying...')
            time.sleep(2)  # Add a small delay before retrying
            clone_current_target(use_cache)  # Retry the whole process
    else:
        if use_cache:
            body['settings']['buildTargetCopyCache'] = new_target_name
            print(f"Using existing cache build target: {new_target_name}")
        else:
            print(f"Not using cache")
        try:
            response = requests.put(f'{URL}/buildtargets/{new_target_name}', headers=HEADERS, json=body)
        except ConnectionError as e:
            print(f'ConnectionError while trying to post exisiting target: {e}. Retrying...')
            time.sleep(2)  # Add a small delay before retrying
            clone_current_target(use_cache)  # Retry the whole process

    print(f"clone_current_target response status: {response.status_code}")
    if response.status_code == 200 or response.status_code == 201:
        # Override target ENV
        os.environ['TARGET'] = new_target_name
        print(f"Copying to TARGET env var. {new_target_name}")
    elif response.status_code == 500 and 'Build target name already in use for this project!' in response.text:
        print('Target update failed due to a possible race condition. Retrying...')
        time.sleep(2)  # Add a small delay before retrying
        clone_current_target(True)  # Retry the whole process
    elif response.status_code == 400:
        print('Target update failed due to incompatible cache file. Retrying...')
        time.sleep(2)  # Add a small delay before retrying
        clone_current_target(False)  # Retry the whole process
    else:
        print('Target failed to clone/update with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(1)

def get_param_env_variables():
    param_variables = {}
    for key, value in os.environ.items():
        if key.startswith("PARAM_"):
            # Remove the "PARAM_" prefix from the key
            param_variables[key[len("PARAM_"):]] = value
    return param_variables

def set_parameters(params):
    hardcoded_params = {
        'TEST_ENV_GIT': 'workflowDefault'
    }
    body = hardcoded_params | params
    url = f'{URL}/buildtargets/{os.getenv("TARGET")}/envvars'
    print(f"Request URL: {url}")
    
    response = requests.put(url, headers=HEADERS, json=body)
    
    if response.status_code == 200:
        print("Parameters set successfully. Response:", response.json())
    else:
        print("Parameters failed with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def get_latest_build(target):
    response = requests.get(f'{URL}/buildtargets/{target}/builds', headers=HEADERS, params={'per_page': 1, 'page': 1})
    
    if response.status_code == 200:
        builds = response.json()
        if builds:
            return builds[0]
    
    print('Failed to get the latest build.')
    return None
    
def run_build(branch, clean):
    max_retries = 10
    retry_delay = 30  # seconds

    print(f'Triggering build for {branch}, clean build = {clean}')
    for attempt in range(max_retries):
        body = {
            'branch': branch,
            'clean' : clean
        }
        try:
            response = requests.post(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds', headers=HEADERS, json=body)

            if response.status_code == 202:
                response_json = response.json()
                print(f'Build response (attempt {attempt + 1}):', response_json)
                
                if 'error' in response_json[0] and 'already a build pending' in response_json[0]['error']:
                    print('A build is already pending. Attempting to cancel it...')
                    latest_build = get_latest_build(os.getenv('TARGET'))
                    if latest_build:
                        cancel_build(latest_build['build'])
                        print(f'Waiting {retry_delay} seconds before retrying...')
                        time.sleep(retry_delay)
                    else:
                        print('Failed to get the latest build ID.')
                        if attempt == max_retries - 1:
                            print('Max retries reached. Exiting.')
                            sys.exit(1)
                elif 'build' in response_json[0]:
                    print('Build started successfully.')
                    return int(response_json[0]['build'])
                else:
                    print('Unexpected response format.')
                    if attempt == max_retries - 1:
                        print('Max retries reached. Exiting.')
                        sys.exit(1)
            else:
                print(f'Build failed to start with status code: {response.status_code}')
                print('Response body:', response.text)
                if attempt == max_retries - 1:
                    print('Max retries reached. Exiting.')
                    sys.exit(1)
        except requests.exceptions.RequestException as e:
            print(f'An exception occurred while trying to start the build (potentially due to a forced socket closure): {e}')
            if attempt == max_retries - 1:
                print('Max retries reached. Exiting.')
                sys.exit(1)
        
        print(f'Retrying... (attempt {attempt + 2} of {max_retries})')
        time.sleep(retry_delay)
    
    print('Failed to start build after maximum retries.')
    sys.exit(1)
    
def cancel_build(id):
    response = requests.delete(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)

    if response.status_code == 204:
        print('Build canceled successfully')
    else:
        print("Build failed to cancel with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def poll_build(id):
    if id == -1:
        print('Error: No build ID known (-1)')
        sys.exit(1)

    retries = 0
    max_retries = 5
    wait_time = 2
    while retries < max_retries:
        try:
            response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}', headers=HEADERS)
            break
        except Exception as e:
            print(f'Request failed: {e}')
            retries += 1
            if retries < max_retries:
                print(f'Retrying in {wait_time} seconds...')
                time.sleep(wait_time)
                wait_time *= 2  # Increase wait time exponentially for each retry
            else:
                raise Exception(f'Failed after {max_retries} retries')

    if response.status_code != 200:
        print(f'Failed to poll build with ID {id} with status code: {response.status_code}')
        print('Response body:', response.text)
        sys.exit(1)

    global build_healthy
    response_json = response.json()

    # { created , queued , sentToBuilder , started , restarted , success , failure , canceled , unknown }
    status = response_json['buildStatus']
    match status:
        case 'created' | 'queued' | 'sentToBuilder' | 'started' | 'restarted':
            print(f'Build status: {status}')
            return True
        case 'success':
            print(f'Build finished successfully! | Elapsed (Unity) time: {datetime.timedelta(seconds=(response_json['totalTimeInSeconds']))}')
            return False
        case 'failure' | 'canceled' | 'unknown':
            print(f'Build error! Last known status: "{status}"')
            build_healthy = False
            return False
        case _:
            print(f'Build status is not known!: "{status}"')
            sys.exit(1)
            return False
            
def download_artifact(id):
    response = requests.get(f'{URL}/buildtargets/{os.getenv("TARGET")}/builds/{id}', headers=HEADERS)

    if response.status_code != 200:
        print(f'Failed to get build artifacts with ID {id} with status code: {response.status_code}')
        print("Response body:", response.text)
        sys.exit(1)

    response_json = response.json()
    try:
        artifact_url = response_json['links']['download_primary']['href']
    except KeyError:
        print(f'Failed to locate any build artifacts - Nothing to download')
        return

    download_dir = 'build'
    filepath = os.path.join(download_dir, 'artifact.zip')

    # Print current working directory and target download directory
    print(f"Current working directory: {os.getcwd()}")
    print(f"Target download directory: {os.path.join(os.getcwd(), download_dir)}")

    os.makedirs(download_dir, exist_ok=True)

    print(f'Started downloading artifacts from Unity Cloud to {download_dir}...')
    response = requests.get(artifact_url)
    with open(filepath, 'wb') as f:
        f.write(response.content)

    print(f'Started extracting artifacts from Unity Cloud to {download_dir}...')
    try:
        with ZipFileWithPermissions(filepath, 'r') as zip_ref:
            zip_ref.extractall(download_dir)

        # Check if this is a macOS target and verify we have the right permissions set
        if 'macos' in os.getenv('TARGET', '').lower():
            explorer_path = os.path.join(download_dir, 'Decentraland.app', 'Contents', 'MacOS', 'Explorer')
            if os.path.exists(explorer_path):
                is_executable = os.access(explorer_path, os.X_OK)
                print(f"Is Explorer executable? {'Yes' if is_executable else 'No'}")
                print(f"Explorer permissions: {oct(os.stat(explorer_path).st_mode)}")
            else:
                print(f"Warning: Explorer executable not found at {explorer_path}")
        else:
            print("Not a macOS target, skipping Explorer executable check.")

    except zipfile.BadZipFile as e:
        print(f'Failed to unzip the artifact at {filepath}: {e}')
        sys.exit(1)
    except Exception as e:
        print(f'An unexpected error occurred during the extraction: {e}')
        sys.exit(1)

    os.remove(filepath)
    print('Artifacts ready!')

    # Final check to confirm build folder exists
    if os.path.exists(download_dir):
        print(f"Build folder confirmed at: {os.path.join(os.getcwd(), download_dir)}")
    else:
        print(f"ERROR: Build folder not found at expected location: {os.path.join(os.getcwd(), download_dir)}")

def download_log(id):
    response = requests.get(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}/log', headers=HEADERS)

    if response.status_code != 200:
        print(f'Failed to get build log with ID {id} with status code: {response.status_code}')
        print("Response body:", response.text)
        sys.exit(1)

    with open('unity_cloud_log.log', 'w') as f:
        f.write(response.text)

    print('Build log ready!')

def delete_build(id):
    response = requests.delete(f'{URL}/buildtargets/{os.getenv('TARGET')}/builds/{id}/artifacts', headers=HEADERS)

    if response.status_code == 200:
        print('Build (on cloud) deleted successfully')
    else:
        print('Build (on cloud) failed to be deleted with status code:', response.status_code)
        print('Response body:', response.text)
        sys.exit(1)

def get_any_running_builds(target, trueOnError = True):
    response = requests.get(f'{URL}/buildtargets/{target}/builds?buildStatus=created,queued,sentToBuilder,started,restarted', headers=HEADERS)

    if response.status_code == 200:
        response_json = response.json()
        if response_json == []:
            return False
        else:
            print('Found at least one running build on build target')
            return True
    else:
        print('Failed to check running builds on build target with status code:', response.status_code)
        print('Response body:', response.text)
        if trueOnError:
            print('Failover - Assuming at least one running, returning True')
            return True
        else:
            sys.exit(1)

def delete_current_target():

    # List of targets to delete
    targets = ['macos', 'windows64']
    
    # Loop through each target
    for target in targets:
        base_target_name = f'{target}-{re.sub("[^A-Za-z0-9]+", "-", os.getenv("BRANCH_NAME"))}'.lower()
        response = requests.delete(f'{URL}/buildtargets/{base_target_name}', headers=HEADERS)
        
        if response.status_code == 204:
            print(f'Build target deleted successfully: "{base_target_name}"')
        elif response.status_code == 404:
            print(f'Build target not found: "{base_target_name} - skip deletion"')
        else:
            print('Build target failed to be deleted with status code:', response.status_code)
            print('Response body:', response.text)
            sys.exit(1)
    
    sys.exit(0)

# Entrypoint here ->
args = parser.parse_args()

# MODE: Delete
if args.delete:
    delete_current_target()
# MODE: Resume
elif args.resume or args.cancel:
    build_info = utils.read_build_info()
    if build_info is None:
        sys.exit(1)

    os.environ['TARGET'] = build_info["target"]
    id = build_info["id"]

# MODE: Cancel
    if args.cancel:
        cancel_build(id)

# MODE: Create (default)
else:

    # Validate branch name before proceeding
    branch_name = os.getenv('BRANCH_NAME')
    validate_branch_name(branch_name)

    # Clone current target
    # This will clone the current $TARGET and replace the value in $TARGET with it
    # Also sets the branch to $BRANCH_NAME
    #
    # If the target already exists, it will check if it has running builds on it
    # If it has running builds, a new target will be created with an added timestamp (Unity can't queue)
    try:
        clone_current_target(True)
    except Exception as e:
        print(f"Operation failed: {e}")

    # Set ENVs (Parameters)
    # This must run immediately before starting a build
    # to avoid any race conditions with other concurrent builds*
    #
    # *Above warning mostly applies to shared targets, not clones
    set_parameters(get_param_env_variables())

    def get_clean_build_bool():
        value = os.getenv('CLEAN_BUILD', 'false').lower() 
        if value in ['true', '1']:
            return True
        elif value in ['false', '0']:
            return False
        else:
            raise ValueError(f"Invalid boolean value for CLEAN_BUILD: {value}")
    # Run Build
    id = run_build(os.getenv('BRANCH_NAME'), get_clean_build_bool())

    utils.persist_build_info(os.getenv('TARGET'), id)
    print(f'For more info and live logs, go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')

# Poll the build stats every {POLL_TIME}s
start_time = time.time()
while True:
    if poll_build(id):
        print(f'Runner elapsed time: {datetime.timedelta(seconds=(time.time() - start_time))} | Polling again in {POLL_TIME}s [...]')
        time.sleep(POLL_TIME)
    else:
        print(f'Runner FINAL elapsed time: {datetime.timedelta(seconds=(time.time() - start_time))}')
        break

# Handle build artifact
download_artifact(id)
# Handle build log
download_log(id)

if not build_healthy:
    print(f'Build unhealthy - check the downloaded logs or go to https://cloud.unity.com/ and search for target "{os.getenv('TARGET')}" and build ID "{id}"')
    sys.exit(1)

# Cleanup (only if build is healthy and not release)
# We only delete all artifacts, not the build target
if not is_release_workflow:
    delete_build(id)

utils.delete_build_info()
