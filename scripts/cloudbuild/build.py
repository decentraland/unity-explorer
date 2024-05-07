import os
import base64
import requests

URL = f'https://build-api.cloud.unity3d.com/api/v1/orgs/{os.getenv('ORG_ID')}/projects/{os.getenv('PROJECT_ID')}'
URL_TARGET = f'{URL}/buildtargets/{os.getenv('TARGET')}'
URL_BUILD = f'{URL_TARGET}/builds'
URL_ENVVARS = f'{URL_TARGET}/envvars'

def create_headers(api_key):
    # Encoding API key in Base64 format
    credentials = f"{api_key}:"
    encoded_credentials = base64.b64encode(credentials.encode('utf-8')).decode('utf-8')
    
    return {
        'Authorization': f'Basic {encoded_credentials}',
        'Content-Type': 'application/json'
    }

headers = create_headers(os.getenv('API_KEY'))

# Set ENVs
body = {
    'TEST_ENV_GIT': 'workflowDefault'
}
response = requests.put(URL_ENVVARS, headers=headers, json=body)
print("Response:", response.json())

# Run Build
body = {
    'branch': os.getenv('BRANCH_NAME'),
    # 'commit': '7d6423555eb96a1e7208adec2b8b7e2f74f1a18f'
}
response = requests.post(URL_BUILD, headers=headers, json=body)
print("Response:", response.json())

# Get build target
# response = requests.get(URL_TARGET, headers=headers)
# print("Response:", response.json())