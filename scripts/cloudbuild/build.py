import os
import re
import sys
import base64
import requests

URL = f'https://build-api.cloud.unity3d.com/api/v1/orgs/{os.getenv('ORG_ID')}/projects/{os.getenv('PROJECT_ID')}'
URL_BUILD_TARGETS = f'{URL}/buildtargets'
URL_TARGET = f'{URL_BUILD_TARGETS}/{os.getenv('TARGET')}'
URL_BUILD = f'{URL_TARGET}/builds'
URL_ENVVARS = f'{URL_TARGET}/envvars'
URL_BUILD_ID = f'{URL_BUILD}/{build_id}'
POLL_TIME = 30 # Seconds

build_id = -1;

def create_headers(api_key):
    # Encoding API key in Base64 format
    credentials = f"{api_key}:"
    encoded_credentials = base64.b64encode(credentials.encode('utf-8')).decode('utf-8')
    
    return {
        'Authorization': f'Basic {encoded_credentials}',
        'Content-Type': 'application/json'
    }

headers = create_headers(os.getenv('API_KEY'))

def get_target_data():
    response = requests.get(URL_TARGET, headers=headers)

    if response.status_code == 200:
        return response.json()
    else:
        print("Failed to get target data with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def clone_current_target():
    body = get_target_data()
    # Set target name based on branch
    clone_target = f'{re.sub(r'^@T_', '', os.getenv('TARGET'))}-{re.sub('[^A-Za-z0-9]+', '', os.getenv('BRANCH_NAME'))}'
    body['name'] = clone_target
    body['settings']['scm']['branch'] = os.getenv('BRANCH_NAME')

    response = requests.post(URL_BUILD_TARGETS, headers=headers, json=body)

    if response.status_code == 201:
        os.environ['TARGET'] = clone_target
    else:
        print('Target failed to start with status code:', response.status_code)
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
    response = requests.put(URL_ENVVARS, headers=headers, json=body)

    if response.status_code == 200:
        print("Parameters set successfully. Response:", response.json())
    else:
        print("Parameters failed with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def run_build(branch):
    body = {
        'branch': branch,
        # 'commit': '7d6423555eb96a1e7208adec2b8b7e2f74f1a18f'
    }
    response = requests.post(URL_BUILD, headers=headers, json=body)

    if response.status_code == 202:
        response_json = response.json()
        print("Build started successfully. Response:", response_json)
        build_id = int(response_json['build'])
    else:
        print("Build failed to start with status code:", response.status_code)
        print("Response body:", response.text)
        sys.exit(1)

def poll_build():
    if build_id == -1:
        print('Error: No build ID known (-1)')
        return

    response = requests.get(URL_BUILD_ID, headers=headers)

    if response.status_code != 200:
        print(f'Failed to poll build with ID {build_id} with status code: {response.status_code}')
        print("Response body:", response.text)
        sys.exit(1)

    response_json = response.json()
    # { created , queued , sentToBuilder , started , restarted , success , failure , canceled , unknown }
    status = response_json['buildStatus']
    time = response_json['totalTimeInSeconds']

    match status:
        case 'created' | 'queued' | 'sentToBuilder' | 'started' | 'restarted':
            print(f'Build status: {status} | Elapsed time: {time}')
            return True
        case 'success':
            print(f'Build finished! | Elapsed time: {time}')
            return False
        case 'failure' | 'canceled' | 'unknown':
            print(f'Build error! Last known status: "{status}" - Failing...')
            sys.exit(1)
            return False
        case _:
            print(f'Build status is not known: "{status}" - Failing...')
            sys.exit(1)
            return False

# Entrypoint here ->

# Clone current target
# This will clone the current $TARGET and replace the value in $TARGET with it
# Also sets the branch to $BRANCH_NAME
clone_current_target()

# Set ENVs (Parameters)
# This must run immediately before starting a build
# to avoid any race conditions with other concurrent builds*
#
# *Above warning mostly applies to shared targets, not clones
set_parameters(get_param_env_variables())

# Run Build
run_build(os.getenv('BRANCH_NAME'))

# Poll the build stats every {POLL_TIME}s
while True:
    if poll_build():
        time.sleep(POLL_TIME)
    else:
        break