import os,sys,runpy
from pathlib import Path
def set_attr(path,name,value,flags=0,**kwargs):
    Path(str(path)+':'+str(name).replace(':','_')).write_bytes(value)
def get_attr(path,name,**kwargs):
    return Path(str(path)+':'+str(name).replace(':','_')).read_bytes()
os.setxattr=set_attr
os.getxattr=get_attr
script=sys.argv.pop(1)
runpy.run_path(script,run_name='__main__')
