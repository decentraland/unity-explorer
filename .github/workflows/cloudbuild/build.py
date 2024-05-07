import os
import base64
import requests

URL = f'https://build-api.cloud.unity3d.com/api/v1/orgs/{os.getenv('ORG_ID')}/projects/{os.getenv('PROJECT_ID')}'
URL_TARGET = f'{URL}/buildtargets/{os.getenv('TARGET')}'
URL_TARGET_BUILD = f'{URL_TARGET}/builds'

def create_headers(api_key):
    # Encoding API key in Base64 format
    credentials = f"{api_key}:"
    encoded_credentials = base64.b64encode(credentials.encode('utf-8')).decode('utf-8')
    
    return {
        'Authorization': f'Basic {encoded_credentials}',
        'Content-Type': 'application/json'
    }

headers = create_headers(os.getenv('API_KEY'))

# Get build target
# response = requests.get(URL_TARGET, headers=headers)
# print("Response:", response.json())

# Run Build
response = requests.get(URL_TARGET_BUILD, headers=headers)
print("Response:", response.json())