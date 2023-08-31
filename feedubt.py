# %%
import parserubt as parser
from pyopenmensa import feed as omfeed


# %%
canteen = omfeed.LazyBuilder()
canteen.additionalCharges()